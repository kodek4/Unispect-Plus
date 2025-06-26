using System;

namespace Unispect.CLI.Helpers
{
    public class ProgressBar : IDisposable
    {
        private readonly int _width = 50;
        private bool _disposed = false;
        private bool _started = false; // ensures first draw starts on a fresh line
        private bool _completed = false;

        public bool Completed => _completed;

        public void Report(double percentage, string status = "")
        {
            if (_disposed) return;

            // If output is redirected, write progress to the Error stream to avoid garbling piped output.
            // Otherwise, write to the standard Output stream.
            var stream = Console.IsOutputRedirected ? Console.Error : Console.Out;

            if (!_started)
            {
                // make sure we begin progress bar on its own line to avoid cluttering with previous output
                stream.WriteLine();
                _started = true;
            }

            var progress = (int)(percentage * _width / 100);
            var bar = new string('█', progress) + new string('░', _width - progress);
            
            stream.Write($"\r[{bar}] {percentage:F1}% {status}");
            
            if (percentage >= 100)
            {
                stream.WriteLine();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        public void UpdateProgress(int percentage)
        {
            Report(percentage);
        }

        public void Complete()
        {
            Report(100, "Complete");
            _completed = true;
        }
    }
} 