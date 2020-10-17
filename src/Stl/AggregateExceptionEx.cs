using System;

namespace Stl
{
    public static class AggregateExceptionEx
    {
        public static Exception GetFirstInnerException(this AggregateException exception)
        {
            while (exception.InnerExceptions.Count > 0) {
                var e = exception.InnerExceptions[0];
                if (e is AggregateException ae)
                    exception = ae;
                else
                    return e;
            }
            return exception;
        }

    }
}
