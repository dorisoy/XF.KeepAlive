# XF.KeepAlive
Xamarin.Android 保活方案, AndroidX 快速实现

```csharp
public override void OnCreate()
{
            base.OnCreate();

            AndroidEnvironment.UnhandledExceptionRaiser += AndroidEnvironment_UnhandledExceptionRaiser;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            //AppUtils
            AppUtils.Init(this);

            //Crash
            CrashHandler.GetInstance().Init(this);

            //1像素广播注册
            KeepManager.GetInstance().RegisterKeep(this);

            //前台服务保活
            StartService(new Intent(this, typeof(ForegroundService)));

            //开启粘性服务进行保活
            StartService(new Intent(this, typeof(StickyService)));

            //使用JobScheduler进行保活
            AliveJobService.StartJob(this);

            //开启保活工作
            StartKeepWork();
}
```
