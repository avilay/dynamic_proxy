using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;

namespace DynamicProxy
{
    public class DelegateInfo
    {
        public MethodInfo InterfaceMethod;
        public Type DelegateType;
        public FieldBuilder DelegateField;
    }

    /// <summary> 
    /// ProxyFactory generates a proxy for any given interface. There are two basic inputs it needs to generate the proxy, the interface to proxy, and 
    /// the real object implementing that interface. This is because, by default the proxy will wire up its interface implementation to the real object's
    /// implementation. Then at runtime the caller can change the behavior of any single method of the interface by asking the proxy to substitute it for 
    /// some caller created method. Note, the other non-substituted methods will remain wired up to the real object.
    /// To illustrate: Say there is an interface called Iinterface which has two methods Foo and Bar. realObj is the object that implements this interface.
    /// ProxyFactory will generate a proxy that also implements Iinterface, but calls realObj.Foo and realObj.Bar as its implementation. 
    /// The caller can then ask proxy to substitute its Bar implementation with some other method MyBar. Now, proxy implements caller.MyBar and realObj.Foo.
    /// 
    /// Gaps:
    /// 1. If an object implements multiple interfaces, the caller will have to create a different proxy for each interface.
    /// 2. If an interface has overloaded methods, the proxy will get confused and will call the first method in the call stack instead of finding the
    /// correct overload.
    /// </summary>
    /// <example>
    /// public interface ICookieService {
    ///     Cookie[] Bake();
    ///     void DistributeAll()
    /// };
    /// 
    /// public class CookieService : ICookieService { ... }
    /// 
    /// class Program {
    ///     public void MyDistributeAll() { some other implementation }
    ///     
    ///     public static void Main() {
    ///         CookieService svc = new CookieService();
    ///         ProxyFactory<ICookieService> pf = new ProxyFactory<ICookieService>(false);
    ///         ICookieService proxy = pf.Create(svc);
    ///         
    ///         ProxyFactory<ICookieService>.ChangeBehavior(proxy, "DistributeAll", new Program(), "MyDistributeAll");
    ///         proxy.Bake(); --> goes to svc.Bake()
    ///         proxy.DistributeAll(); --> goes to MyDistributeAll()                        
    ///     }
    /// }
    /// </example>
    /// <remarks>
    /// See the UsageExample project to see a working example based on the example expained in the examples section.
    /// </remarks>
    /// <typeparam name="T">The interface to be proxied</typeparam>
    public class ProxyFactory<T>
    {
        private AssemblyBuilder _assemblyBuilder;
        private ModuleBuilder _moduleBuilder;
        private Type _proxyType;
        private string _assemblyName;
        private string _moduleName;
        private string _proxyName;
        private string _binName;
        private bool _save;


        public AssemblyBuilder MyAssembly {
            get {
                return this._assemblyBuilder;
            }
        }

        private void SetupAssembly() {
            Type realIf = typeof(T);
            string entityName = realIf.Name.TrimStart('I');
            _assemblyName = entityName + "Assembly";
            _moduleName = entityName + "Module";
            _proxyName = entityName + "Proxy";
            _binName = _assemblyName + ".dll";

            AppDomain currentDomain = AppDomain.CurrentDomain;
            AssemblyName assemblyName = new AssemblyName();
            assemblyName.Name = _assemblyName;
            if (_save) {
                _assemblyBuilder = currentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
                _moduleBuilder = _assemblyBuilder.DefineDynamicModule(_moduleName, _binName);
            }
            else {
                _assemblyBuilder = currentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                _moduleBuilder = _assemblyBuilder.DefineDynamicModule(_moduleName);
            }
        }

        public ProxyFactory(bool save) {
            Type realIf = typeof(T);
            if (!realIf.IsInterface) throw new Exception("Type must be an interface");

            _save = save;

            SetupAssembly();

            // Build the delegates
            List<DelegateInfo> delegates = new List<DelegateInfo>();
            foreach (MethodInfo mi in realIf.GetMethods()) {
                string delegateName = mi.Name + "Delegate";
                TypeBuilder db = _moduleBuilder.DefineType(delegateName,
                    TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoClass,
                    typeof(MulticastDelegate));
                ConstructorBuilder delegateCtorBldr = db.DefineConstructor(
                    MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                    CallingConventions.Standard,
                    new Type[] { typeof(object), typeof(System.IntPtr) });
                delegateCtorBldr.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

                // Grab the parameters of the method
                ParameterInfo[] parameters = mi.GetParameters();
                Type[] paramTypes = new Type[parameters.Length];

                for (int i = 0; i < parameters.Length; i++) {
                    paramTypes[i] = parameters[i].ParameterType;
                }
                // Define the Invoke method for the delegate
                MethodBuilder methodBuilder = db.DefineMethod(
                    "Invoke",
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                    mi.ReturnType,
                    paramTypes);
                methodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

                DelegateInfo di = new DelegateInfo();
                di.InterfaceMethod = mi;
                di.DelegateType = db.CreateType();
                delegates.Add(di);
            }

            string proxyTypeName = realIf.Name.TrimStart('I') + "Proxy";
            TypeBuilder proxyBuilder = _moduleBuilder.DefineType(proxyTypeName, TypeAttributes.Class | TypeAttributes.Public);
            proxyBuilder.AddInterfaceImplementation(realIf);

            //Add delegates as properties of the proxy
            for (int i = 0; i < delegates.Count; i++) {
                DelegateInfo di = delegates[i];
                FieldBuilder fb = proxyBuilder.DefineField(di.DelegateType.Name, di.DelegateType, FieldAttributes.Public);
                di.DelegateField = fb;
            }

            FieldBuilder rsField = proxyBuilder.DefineField("_realService", realIf, FieldAttributes.Private);

            //Emit the constructor
            Type[] ctorArgs = { realIf };
            ConstructorInfo baseCtor = typeof(object).GetConstructor(new Type[0]);
            ConstructorBuilder ctorBuilder = proxyBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, ctorArgs);
            ILGenerator ctorIl = ctorBuilder.GetILGenerator();
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Call, baseCtor);
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Ldarg_1);
            ctorIl.Emit(OpCodes.Stfld, rsField);

            foreach (DelegateInfo di in delegates) {
                ctorIl.Emit(OpCodes.Ldarg_0);
                ctorIl.Emit(OpCodes.Ldarg_0);
                ctorIl.Emit(OpCodes.Ldfld, rsField);
                ctorIl.Emit(OpCodes.Dup);
                ctorIl.Emit(OpCodes.Ldvirtftn, di.InterfaceMethod);
                ctorIl.Emit(OpCodes.Newobj, di.DelegateType.GetConstructor(new Type[] { typeof(object), typeof(System.IntPtr) }));
                ctorIl.Emit(OpCodes.Stfld, di.DelegateField);
            }

            ctorIl.Emit(OpCodes.Ret);

            //Implement the interface
            foreach (DelegateInfo di in delegates) {
                // Grab the parameters of the method
                ParameterInfo[] parameters = di.InterfaceMethod.GetParameters();
                Type[] paramTypes = new Type[parameters.Length];

                for (int i = 0; i < parameters.Length; i++) {
                    paramTypes[i] = parameters[i].ParameterType;
                }

                MethodBuilder methodBuilder = proxyBuilder.DefineMethod(
                    di.InterfaceMethod.Name,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final,
                    di.InterfaceMethod.ReturnType,
                    paramTypes);
                methodBuilder.SetImplementationFlags(MethodImplAttributes.IL | MethodImplAttributes.Managed);

                ILGenerator methodIl = methodBuilder.GetILGenerator();
                if (!di.DelegateType.GetMethod("Invoke").ReturnType.Equals(typeof(void))) {
                    LocalBuilder loc = methodIl.DeclareLocal(di.DelegateType.GetMethod("Invoke").ReturnType);
                }
                Label endOfMthd = methodIl.DefineLabel();
                methodIl.Emit(OpCodes.Ldarg_0);
                methodIl.Emit(OpCodes.Ldfld, di.DelegateField);
                for (int i = 1; i <= parameters.Length; i++) {
                    methodIl.Emit(OpCodes.Ldarg, (short)i);
                }
                methodIl.Emit(OpCodes.Callvirt, di.DelegateType.GetMethod("Invoke"));
                if (!di.DelegateType.GetMethod("Invoke").ReturnType.Equals(typeof(void))) {
                    methodIl.Emit(OpCodes.Stloc_0);
                    methodIl.Emit(OpCodes.Br_S, endOfMthd);
                    methodIl.MarkLabel(endOfMthd);
                    methodIl.Emit(OpCodes.Ldloc_0);
                }
                methodIl.Emit(OpCodes.Ret);
            }

            _proxyType = proxyBuilder.CreateType();

            if (_save) _assemblyBuilder.Save(_binName);
        }

        public T Create(T real) {
            return (T)Activator.CreateInstance(_proxyType, new object[] { real });
        }

        public static void ChangeBehavior(T proxy, string methodNameToChange, object target, string targetMethod) {
            string delegateName = methodNameToChange + "Delegate";
            FieldInfo delegateField = proxy.GetType().GetField(delegateName);
            Delegate delegateObj = Delegate.CreateDelegate(delegateField.FieldType, target, target.GetType().GetMethod(targetMethod));
            delegateField.SetValue(proxy, delegateObj);
        }
    }
}
