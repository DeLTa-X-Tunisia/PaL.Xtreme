using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PaLX.Admin.Services
{
    public class ProgressableStreamContent : HttpContent
    {
        private const int DefaultBufferSize = 4096;
        private readonly Stream _content;
        private readonly int _bufferSize;
        private readonly IProgress<int> _progress;

        public ProgressableStreamContent(Stream content, int bufferSize, IProgress<int> progress)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _bufferSize = bufferSize > 0 ? bufferSize : DefaultBufferSize;
            _progress = progress;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[_bufferSize];
            var totalBytes = _content.Length;
            var uploadedBytes = 0L;

            using (_content)
            {
                while (true)
                {
                    var length = await _content.ReadAsync(buffer, 0, _bufferSize);
                    if (length <= 0) break;

                    await stream.WriteAsync(buffer, 0, length);
                    uploadedBytes += length;

                    _progress?.Report((int)(uploadedBytes * 100 / totalBytes));
                }
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }
    }
}
