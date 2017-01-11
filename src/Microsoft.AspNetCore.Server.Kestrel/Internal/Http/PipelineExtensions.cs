using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Server.Kestrel.Internal.Http
{
    public static class PipelineExtensions
    {
        public static ValueTask<ArraySegment<byte>> PeekAsync(this IPipelineReader pipelineReader)
        {
            var input = pipelineReader.ReadAsync();
            while (input.IsCompleted)
            {
                var result = input.GetResult();
                try
                {
                    if (!result.Buffer.IsEmpty)
                    {
                        ArraySegment<byte> data;
                        var segment = result.Buffer.First;
                        var arrayResult = segment.TryGetArray(out data);
                        Debug.Assert(arrayResult);

                        return new ValueTask<ArraySegment<byte>>(data);
                    }
                    else if (result.IsCompleted || result.IsCancelled)
                    {
                        return default(ValueTask<ArraySegment<byte>>);
                    }
                }
                finally
                {
                    pipelineReader.Advance(result.Buffer.Start, result.Buffer.Start);
                }
                input = pipelineReader.ReadAsync();
            }

            return new ValueTask<ArraySegment<byte>>(pipelineReader.PeekAsyncAwaited(input));
        }

        private static async Task<ArraySegment<byte>> PeekAsyncAwaited(this IPipelineReader pipelineReader, ReadableBufferAwaitable readingTask)
        {
            while (true)
            {
                var result = await readingTask;

                await Task.Yield();

                try
                {
                    if (!result.Buffer.IsEmpty)
                    {
                        ArraySegment<byte> data;
                        var segment = result.Buffer.First;
                        var arrayResult = segment.TryGetArray(out data);
                        Debug.Assert(arrayResult);

                        return data;
                    }
                    else if (result.IsCompleted || result.IsCancelled)
                    {
                        return default(ArraySegment<byte>);
                    }
                }
                finally
                {
                    pipelineReader.Advance(result.Buffer.Start, result.Buffer.Start);
                }
                readingTask = pipelineReader.ReadAsync();
            }
        }

        public static async Task<ReadResult> ReadAsyncDispatched(this IPipelineReader pipelineReader)
        {
            var result = await pipelineReader.ReadAsync();
            await Task.Yield();
            return result;
        }

        public static Span<byte> ToSpan(this ReadableBuffer buffer)
        {
            if (buffer.IsSingleSpan)
            {
                return buffer.First.Span;
            }
            else
            {
                // todo: slow
                return buffer.ToArray();
            }
        }
    }
}