using System;

namespace DolomiteModel.EntityFramework
{
    static class ConversionUtilities
    {
        [System.Data.Objects.DataClasses.EdmFunction("DolomiteModel.EntityFramework", "ConvertToInt32")]
        public static int ConvertToInt32(string myStr)
        {
            throw new NotSupportedException("Direct calls are not supported.");
        }

        [System.Data.Objects.DataClasses.EdmFunction("DolomiteModel.EntityFramework", "ConvertToDecimal")]
        public static decimal ConvertToDecimal(string myStr)
        {
            throw new NotSupportedException("Direct calls are not supported.");
        }
    }
}
