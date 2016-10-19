using System;

namespace Slamby.API.Helpers
{
    public class Progress
    {
        private object _lock = new object();
        private int _value;

        /// <summary>
        /// Current value
        /// </summary>
        public int Value {
            get
            {
                return _value;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(Value));
                }

                lock (_lock)
                {
                    _value = value;
                }
            }
        }

        /// <summary>
        /// Maximum value for Current
        /// </summary>
        public int Total { get; }

        /// <summary>
        /// Percent for display e.g.: 85.67
        /// </summary>
        public double Percent => CalculateInner(Value, Total);

        /// <summary>
        /// Percent value e.g.: 0.8567
        /// </summary>
        public double PercentValue => Percent / 100;

        public Progress(int total, int value = 0)
        {
            if (total <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(total));
            }

            Total = total;
            Value = value;
        }

        private double CalculateInner(double value, double total)
        {
            return value / total * 100;
        }

        public double Calculate(double value)
        {
            return CalculateInner(value, Total);
        }

        /// <summary>
        /// Increase the Value property value by 1 and returns the new value.
        /// Required for multi-threaded environment where Value counts.
        /// </summary>
        /// <returns>Increased value</returns>
        public int Step()
        {
            lock (_lock)
            {
                Value = Value + 1;
                return Value;
            }
        }

        /// <summary>
        /// If there are two progresses (parent and child), it can calculate the overall percentage
        /// </summary>
        /// <param name="innerProgress"></param>
        /// <returns></returns>
        public double MultiPercent(Progress innerProgress)
        {
            return Calculate(Value + innerProgress.PercentValue);
        }

        public override string ToString()
        {
            return $"{Math.Round(Percent, 6)}% ({Value}/{Total})";
        }
    }
}
