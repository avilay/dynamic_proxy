using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UsageExample
{
    public interface ICookieService
    {
        Cookie[] Bake();
        void DistributeAll();
    }
}
