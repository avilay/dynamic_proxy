using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UsageExample
{
    public class Cookie
    {
        private string _name;

        public Cookie(string name) {
            _name = name;
        }

        public string Name() {
            return _name;
        }
    }
}
