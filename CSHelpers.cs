using System;

namespace AxonApiHelper
{
    public class CSHelpers
    {
        public static void Try(Action a)
        {
            Try(() => { a.Invoke(); return null; });
        }

        public static dynamic Try(Func<dynamic> f)
        {
            return Try<Exception>(f);
        }

        public static dynamic Try<T>(Func<dynamic> f) where T : Exception
        {
            return Try(f, out T _);
        }

        public static dynamic Try<T>(Func<dynamic> f, out T e) where T : Exception
        {
            try
            {
                dynamic retVal = f.Invoke();
                e = null;
                return retVal;
            }
            catch (T ex)
            {
                e = ex;
                return null;
            }
        }

        public static dynamic Retry(Func<dynamic> functionToRetry, uint maxAttempts, bool safe = true, bool throwLast = false)
        {
            return Retry(functionToRetry, (d) => { return d != null; }, maxAttempts, safe, throwLast);
        }

        public static dynamic Retry(Func<dynamic> functionToRetry, dynamic goodResult, uint maxAttempts, bool safe = true, bool throwLast = false)
        {
            return Retry(functionToRetry, (d) => { return d == goodResult; }, maxAttempts, safe, throwLast);
        }

        public static dynamic Retry(Func<dynamic> functionToRetry, Func<dynamic, bool> outcomeVerification, uint maxAttempts, bool safe = true, bool throwLast = false)
        {
            Exception lastException = null;
            for (int i = 0; i < maxAttempts; i++)
            {
                dynamic result = safe ? Try(functionToRetry, out lastException) : functionToRetry.Invoke();
                if (outcomeVerification.Invoke(result))
                {
                    return result;
                }
            }
            Exception defaultException = new("Max Retry attempts reached");
            throw throwLast ? lastException ?? defaultException : defaultException;
        }
    }
}
