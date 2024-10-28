using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShrimpleDB
{
	public class SDBException : Exception
	{
		public SDBException() { }
		public SDBException(string message) : base(message) { }
	}
}
