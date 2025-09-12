using Stream =  System.IO.Stream;

namespace SoZvon.ServerAPIManager
{
    abstract record Operation(string OperationId, string Filename, string Url, CancellationToken Cts);
    record DownloadOperation : Operation
    {
        public string DestinationPath { get; set; }
        public int ProgressStep { get; set; }
        public DownloadOperation(string OperationId, string Filename, string Url, string DestinationPath, int ProgressStep, CancellationToken Cts) : base(OperationId, Filename, Url, Cts)
        {
            this.DestinationPath = DestinationPath;
            this.ProgressStep = ProgressStep;
        }
    }
    record UploadOperation : Operation
    {
        public int ProgressStep { get; set; }
        public UploadOperation(string OperationId, string Filename, string Url, int ProgressStep, CancellationToken Cts) : base(OperationId, Filename, Url, Cts)
        {
            this.ProgressStep = ProgressStep;
        }
    }
    record GetFileInfoOperation : Operation
    {
        public GetFileInfoOperation(string OperationId, string Filename, string Url, CancellationToken Cts) : base(OperationId, Filename, Url, Cts) { }
    }
    record UploadResult(string filename, string status, string hash);
    record GetFileInfoResult(string name, string hash, string date, long size);
    class ProgressableStreamContent(Stream content, int bufferSize, Action<long> progressCallback) : System.Net.Http.HttpContent
    {
        readonly Stream _content = content;
        readonly int _bufferSize = bufferSize;
        readonly Action<long> _progressCallback = progressCallback;

        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            var buffer = new byte[_bufferSize];
            long totalSent = 0;
            int bytesRead;

            while ((bytesRead = await _content.ReadAsync(buffer)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalSent += bytesRead;
                _progressCallback?.Invoke(totalSent);
            }
        }
        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }
        protected override void Dispose(bool disposing)
        {
            _content?.Dispose();
            base.Dispose(disposing);
        }
    }
}
