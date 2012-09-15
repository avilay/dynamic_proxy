using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UsageExample
{
    public class SnackController
    {
        private ICookieService _svc;

        public SnackController(ICookieService svc) {
            _svc = svc;
        }

        public void PrepareSnacks() {
            Cookie[] cookies = _svc.Bake();
            foreach (Cookie cookie in cookies) {
                Console.WriteLine("Baked cookie " + cookie.Name());
            }
            _svc.DistributeAll();
        }
    }
}
