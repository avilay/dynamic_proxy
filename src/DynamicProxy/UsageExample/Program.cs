using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynamicProxy;

namespace UsageExample
{
    class Program
    {
        // This is the SnackController unit test
        static void Main(string[] args) {
            // Create the proxy cookie service that wraps the real CookieService
            CookieService svc = new CookieService();
            ProxyFactory<ICookieService> pf = new ProxyFactory<ICookieService>(false);
            ICookieService proxy = pf.Create(svc);

            // Construct SnackController with the proxy cookie service
            SnackController ctrlr = new SnackController(proxy);
            ctrlr.PrepareSnacks(); // All calls are routed to the real CookieService

            // Now inject a fault in CookieService.DistributeAll
            ProxyFactory<ICookieService>.ChangeBehavior(proxy, "DistributeAll", 
                new FaultyMethods(), "DistributeAllChanged");
            ctrlr.PrepareSnacks(); // Calls to CookieService.DistributeAll are routed to FaultyMethods.DistributeAllChanged
        }
    }
}
