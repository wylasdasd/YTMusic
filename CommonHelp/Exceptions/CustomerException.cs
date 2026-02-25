using System;
using System.Collections.Generic;
using System.Text;

namespace CommonTool.Exceptions
{
    public class FileBigException : Exception
    {
        public FileBigException(string exMsg) : base(exMsg)
        {
        }

        public FileBigException(string exMsg, Exception ex) : base(exMsg, ex)
        {
        }
    }
}