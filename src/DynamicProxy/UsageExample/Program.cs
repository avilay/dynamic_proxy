using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynamicProxy;

namespace UsageExample
{
    class Program
    {
        public void DistributeAllChanged() {
            Console.WriteLine("Nothing to distribute! Move on!");
        }

        static void Main(string[] args) {
            CookieService svc = new CookieService();
            ProxyFactory<ICookieService> pf = new ProxyFactory<ICookieService>(false);
            ICookieService proxy = pf.Create(svc);

            Cookie[] cookies = proxy.Bake();
            foreach (Cookie cookie in cookies) {
                Console.WriteLine("Baked cookie " + cookie.Name());
            }
            proxy.DistributeAll();

            Program p = new Program();
            ProxyFactory<ICookieService>.ChangeBehavior(proxy, "DistributeAll", p, "DistributeAllChanged");
            proxy.DistributeAll();
        }
    }
}
