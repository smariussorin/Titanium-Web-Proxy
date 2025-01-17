﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Titanium.Web.Proxy.StreamExtended.BufferPool;

namespace Titanium.Web.Proxy.StreamExtended.Network
{
    /// <summary>
    ///     Copies the source stream to destination stream.
    ///     But this let users to peek and read the copying process.
    /// </summary>
    public class CopyStream : ICustomStreamReader, IDisposable
    {
        private readonly ICustomStreamReader reader;

        private readonly ICustomStreamWriter writer;

        private readonly IBufferPool bufferPool;

        private int bufferLength;

        private byte[] buffer;

        private bool disposed;

        public int Available => reader.Available;

        public bool DataAvailable => reader.DataAvailable;

        public long ReadBytes { get; private set; }

        public CopyStream(ICustomStreamReader reader, ICustomStreamWriter writer, IBufferPool bufferPool)
        {
            this.reader = reader;
            this.writer = writer;
            buffer = bufferPool.GetBuffer();
            this.bufferPool = bufferPool;
        }

        public async Task<bool> FillBufferAsync(CancellationToken cancellationToken = default)
        {
            await FlushAsync(cancellationToken);
            return await reader.FillBufferAsync(cancellationToken);
        }

        public byte PeekByteFromBuffer(int index)
        {
            return reader.PeekByteFromBuffer(index);
        }

        public Task<int> PeekByteAsync(int index, CancellationToken cancellationToken = default)
        {
            return reader.PeekByteAsync(index, cancellationToken);
        }

        public Task<int> PeekBytesAsync(byte[] buffer, int offset, int index, int size, CancellationToken cancellationToken = default)
        {
            return reader.PeekBytesAsync(buffer, offset, index, size, cancellationToken);
        }

        public void Flush()
        {
            // send out the current data from from the buffer
            if (bufferLength > 0)
            {
                writer.Write(buffer, 0, bufferLength);
                bufferLength = 0;
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            // send out the current data from from the buffer
            if (bufferLength > 0)
            {
                await writer.WriteAsync(buffer, 0, bufferLength, cancellationToken);
                bufferLength = 0;
            }
        }

        public byte ReadByteFromBuffer()
        {
            byte b = reader.ReadByteFromBuffer();
            buffer[bufferLength++] = b;
            ReadBytes++;
            return b;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int result = reader.Read(buffer, offset, count);
            if (result > 0)
            {
                if (bufferLength + result > bufferPool.BufferSize)
                {
                    Flush();
                }

                Buffer.BlockCopy(buffer, offset, this.buffer, bufferLength, result);
                bufferLength += result;
                ReadBytes += result;
                Flush();
            }

            return result;
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            int result = await reader.ReadAsync(buffer, offset, count, cancellationToken);
            if (result > 0)
            {
                if (bufferLength + result > bufferPool.BufferSize)
                {
                    await FlushAsync(cancellationToken);
                }

                Buffer.BlockCopy(buffer, offset, this.buffer, bufferLength, result);
                bufferLength += result;
                ReadBytes += result;
                await FlushAsync(cancellationToken);
            }

            return result;
        }

        public Task<string> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            return CustomBufferedStream.ReadLineInternalAsync(this, bufferPool, cancellationToken);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                var b = buffer;
                buffer = null;
                bufferPool.ReturnBuffer(b);
            }
        }
    }
}
