using System;

namespace DolomiteModel.EntityFramework
{
    static class ConversionUtilities
    {
        [System.Data.Entity.DbFunction("DolomiteModel.EntityFramework", "ConvertToInt32")]
        public static int ConvertToInt32(string myStr)
        {
            throw new NotSupportedException("Direct calls are not supported.");
        }

        [System.Data.Entity.DbFunction("DolomiteModel.EntityFramework", "ConvertToDecimal")]
        public static decimal ConvertToDecimal(string myStr)
        {
            throw new NotSupportedException("Direct calls are not supported.");
        }
    }
}
