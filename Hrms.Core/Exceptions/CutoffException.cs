using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hrms.Core;
public class CutoffMismatchException : Exception
{
    public CutoffMismatchException(string message) : base(message) { }
}