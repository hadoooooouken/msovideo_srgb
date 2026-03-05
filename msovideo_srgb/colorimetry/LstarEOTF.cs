using System;

namespace msovideo_srgb
{
    public class LstarEOTF : ToneCurve
    {
        private double _black;

        public LstarEOTF(double black)
        {
            _black = black;
        }

        public double SampleAt(double x)
        {
            if (x >= 1) return 1;
            if (x <= 0) return _black;

            const double delta = 6 / 29d;

            x = (x + 0.16) / 1.16;
            
            double result;
            if (x > delta)
            {
                result = x * x * x;
            }
            else
            {
                result = 3 * (delta * delta) * (x - 4 / 29d);
            }

            return result * (1 - _black) + _black;
        }

        public double SampleInverseAt(double x)
        {
            if (_black != 0) throw new NotSupportedException();
            if (x >= 1) return 1;
            if (x <= 0) return 0;

            const double delta = 6.0 / 29.0;

            double fy;
            if (x > delta * delta * delta)
            {
                fy = Math.Pow(x, 1.0 / 3.0);
            }
            else
            {
                fy = x / (3 * delta * delta) + 4.0 / 29.0;
            }

            return 1.16 * fy - 0.16;
        }
    }
}