using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UsageExample
{
    class CookieService : ICookieService
    {
        private Cookie[] _cookies;
        private int _id;
        
        public Cookie[] Bake() {
            Cookie or = new Cookie("Oatmeal Raisin");
            Cookie cc = new Cookie("Chocolate Chip");
            _cookies = new Cookie[] { or, cc };
            return _cookies;
        }

        public void DistributeAll() {
            foreach (Cookie cookie in _cookies) {
                Console.WriteLine("Distributing " + cookie.Name());
            }
        }
        
    }
}
