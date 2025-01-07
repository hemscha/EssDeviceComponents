using EssAppComponents.Common.Classes;
using EssAppComponents.Logging.Interfaces;

namespace EssDeviceComponents.Classes
{
    public class EssManagedDeviceInterface(IEssAppLogger? logger, string? endOfLine = "\r\n")
    {
        protected readonly IEssAppLogger? Logger = logger;
        protected readonly string? EndOfLine = endOfLine;

        protected CancellationTokenSource CancelTokenSource = null!;

        protected readonly object LockObject = new(); // Lock-Objekt für die Synchronisation

        protected bool HasReceivedResponse;
        protected bool IsWaitingPeriodExpired;

        protected async void WaitForResponse(uint waitMilliseconds)
        {
            HasReceivedResponse = false;
            IsWaitingPeriodExpired = false;

            if (waitMilliseconds < 100)
            {
                return;
            }

            // Round up to the next 500 milliseconds
            uint realWaitMilliseconds = (uint)Math.Ceiling(waitMilliseconds / 1000.0) * 1000;

            // Clamp to the range [MIN_DURATION, MAX_DURATION]
            realWaitMilliseconds = Math.Clamp(realWaitMilliseconds, 1000, 10000);

            var stopAt = DateTime.Now.AddMilliseconds(realWaitMilliseconds);

            while (HasReceivedResponse == false && DateTime.Now < stopAt && CancelTokenSource.IsCancellationRequested == false)
            {
                await EssTask.DelayAsync(100, CancelTokenSource.Token);
            }

            if (HasReceivedResponse == false)
            {
                IsWaitingPeriodExpired = true;
                var seconds = realWaitMilliseconds / 1000;
                var secondsText = seconds == 1 ? "second" : "seconds";
                Logger?.Debug($"No response received after {seconds} {secondsText}.");
            }

        }

        protected string StripEndOfLineCharacter(string data)
        {
            // If data is empty or _endOfLine is empty, return the data as is

            // Important!!!
            // If _endOfLine is “\r\n” or “\n”, the string.IsNullOrWhiteSpace(_endOfLine) method returns true!!!
            // Therefore: Check with string.IsNullOrEmpty(_endOfLine) whether _endOfLine is valid!!!
            if (string.IsNullOrWhiteSpace(data) || string.IsNullOrEmpty(EndOfLine))
            {
                return data;
            }

            string realData = data.Trim();

            // If the data ends with the character(s) for the new line (see property _serialPort.NewLine), it will be removed.
            if (realData.EndsWith(EndOfLine, StringComparison.OrdinalIgnoreCase))
            {
                // Remove trailing white spaces after removing the new line character
                realData = realData[..^EndOfLine.Length].TrimEnd();
            }

            return realData;
        }


    }
}
