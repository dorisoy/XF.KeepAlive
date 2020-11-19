using Android.App;
using Android.App.Job;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Text;
using Android.Util;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Work;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;



namespace DCMS.Client.Droid
{
    /// <summary>
    /// 使用 ShinyAndroidApplication 接替 Application
    /// </summary>
    [Application(LargeHeap = true)]
    public class MainApplication : ShinyAndroidApplication<Startup>
    {
        private static MainApplication instance;
        public static Activity activity;

        public static MainApplication GetInstance()
        {
            return instance;
        }

        public MainApplication(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
            instance = this;
        }

        public override void OnCreate()
        {
            base.OnCreate();

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

        private static readonly string TAG = "KeepLiveWork";
        private static readonly string TAG_KEEP_WORK = "KeepLiveWork";
        public void StartKeepWork()
        {
            WorkManager.GetInstance(this).CancelAllWorkByTag(TAG_KEEP_WORK);
            Log.Debug(TAG, "keep-> dowork startKeepWork");
            OneTimeWorkRequest oneTimeWorkRequest = new OneTimeWorkRequest.Builder(typeof(KeepLiveWork))
                .SetBackoffCriteria(AndroidX.Work.BackoffPolicy.Linear, 5, Java.Util.Concurrent.TimeUnit.Seconds)
                .AddTag(TAG_KEEP_WORK)
                .Build();
            WorkManager.GetInstance(this).Enqueue(oneTimeWorkRequest);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            var newExc = new System.Exception("TaskSchedulerOnUnobservedTaskException", e.Exception);
            FileUtils.WriteLog(newExc);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var newExc = new System.Exception("CurrentDomainOnUnhandledException", e.ExceptionObject as System.Exception);
            FileUtils.WriteLog(newExc);
        }

        private void AndroidEnvironment_UnhandledExceptionRaiser(object sender, RaiseThrowableEventArgs e)
        {
            var newExc = new System.Exception("UnhandledExceptionRaiser", e.Exception);
            FileUtils.WriteLog(newExc);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            AndroidEnvironment.UnhandledExceptionRaiser -= AndroidEnvironment_UnhandledExceptionRaiser;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
        }
    }

    #region workmanager

    /// <summary>
    /// 利用jetpack中的workManager启动WorkJobService进行保活
    /// </summary>
    public class KeepLiveWork : Worker
    {
        private Context _context;
        public KeepLiveWork(Context context, WorkerParameters workerParams) : base(context, workerParams)
        {
            _context = context;
        }

        public override Result DoWork()
        {
            try
            {
                //启动job服务
                WorkJobService.StartJob(_context);

                //启动相互绑定的服务
                //StartKeepService();

                return new Result.Retry();
            }
            catch (Exception)
            {
                return new Result.Failure();
            }
        }
    }
    public class WorkJobService : JobService
    {
        private static readonly string TAG = "WorkJobService";

        public static void StartJob(Context context)
        {
            Log.Error(TAG, "startJob");
            JobScheduler jobScheduler = (JobScheduler)context.GetSystemService(Context.JobSchedulerService);
            JobInfo.Builder builder = new JobInfo.Builder(8, new ComponentName(context.PackageName,
                   typeof(WorkJobService).Name)).SetPersisted(true);

            // 小于7.0
            if (Build.VERSION.SdkInt < BuildVersionCodes.N)
            {
                builder.SetPeriodic(1000);
            }
            else
            {
                builder.SetMinimumLatency(1000);
            }

            jobScheduler.Schedule(builder.Build());
        }


        public override bool OnStartJob(JobParameters jobParameters)
        {
            Log.Error(TAG, "onStartJob");

            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                StartJob(this);
            }

            return false;
        }

        public override bool OnStopJob(JobParameters jobParameters)
        {
            //base.OnStopJob(jobParameters);
            return false;
        }

    }

    #endregion

    #region jobscheduler

    public class AliveJobService : JobService
    {
        private static readonly string TAG = "AliveJobService";
        public static void StartJob(Context context)
        {
            JobScheduler jobScheduler = (JobScheduler)context.GetSystemService(Context.JobSchedulerService);
            //setPersisted 在设备重启依然执行
            // 需要增加权限 RECEIVE_BOOT_COMPLETED
            JobInfo.Builder builder = new JobInfo.Builder(8, new ComponentName(context.PackageName,
                      typeof(AliveJobService).Name)).SetPersisted(true);

            // 小于7.0
            if (Build.VERSION.SdkInt < BuildVersionCodes.N)
            {
                // 每隔 1s 执行一次 job
                // 版本23 开始 进行了改进，最小周期为 5s
                builder.SetPeriodic(1000);
            }
            else
            {
                // 延迟执行任务
                builder.SetMinimumLatency(1000);
            }
            jobScheduler.Schedule(builder.Build());
        }

        public override bool OnStartJob(JobParameters jobParameters)
        {
            Log.Error(TAG, "onStartJob");

            // 如果7.0以上 轮询
            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                StartJob(this);
            }

            return false;
        }

        public override bool OnStopJob(JobParameters jobParameters)
        {
            return false;
        }
    }

    #endregion

    #region service

    /// <summary>
    /// 使用粘性服务进行保活
    /// </summary>
    [Service]
    public class StickyService : Service
    {
        public override IBinder OnBind(Intent intent)
        {
            return null;
        }
        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            return base.OnStartCommand(intent, flags, startId);
            //return StartCommandResult.Sticky;
        }
        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }


    /// <summary>
    /// 不同版本有差异，需分开处理 此种方法适用于音乐播放器保活，8.0以后会在通知栏显示
    /// </summary>
    [Service]
    public class ForegroundService : Service
    {
        private static readonly string TAG = "ForegroundService";
        private static readonly int SERVICE_ID = 1;

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override void OnCreate()
        {
            base.OnCreate();

            Log.Error(TAG, "ForegroundService 服务创建了");

            if (Build.VERSION.SdkInt < BuildVersionCodes.JellyBeanMr2)
            {
                //4.3以下
                //将service设置成前台服务，并且不显示通知栏消息
                StartForeground(SERVICE_ID, new Notification());
            }
            else if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
                //Android4.3-->Android7.0
                //将service设置成前台服务
                StartForeground(SERVICE_ID, new Notification());
                //删除通知栏消息
                StartService(new Intent(this, typeof(InnerService)));
            }
            else
            {
                // 8.0 及以上
                //通知栏消息需要设置channel
                NotificationManager manager = (NotificationManager)GetSystemService(NotificationService);
                //NotificationManager.IMPORTANCE_MIN 通知栏消息的重要级别  最低，不让弹出
                //IMPORTANCE_MIN 前台时，在阴影区能看到，后台时 阴影区不消失，增加显示 IMPORTANCE_NONE时 一样的提示
                //IMPORTANCE_NONE app在前台没有通知显示，后台时有
                NotificationChannel channel = new NotificationChannel("channel", "xx", NotificationImportance.None);
                if (manager != null)
                {
                    manager.CreateNotificationChannel(channel);
                    Notification notification = new NotificationCompat.Builder(this, "channel").Build();
                    //将service设置成前台服务，8.x退到后台会显示通知栏消息，9.0会立刻显示通知栏消息
                    StartForeground(SERVICE_ID, notification);
                }
            }
        }

        /// <summary>
        /// 内联服务
        /// </summary>
        public class InnerService : Service
        {
            public override void OnCreate()
            {
                base.OnCreate();

                Log.Error(TAG, "InnerService 服务创建了");
                // 让服务变成前台服务
                StartForeground(SERVICE_ID, new Notification());
                // 关闭自己
                StopSelf();
            }

            public override IBinder OnBind(Intent intent)
            {
                return null;
            }

            public override void OnDestroy()
            {
                base.OnDestroy();
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }


    #endregion

    #region activity

    /// <summary>
    /// 用于保活的1像素activity
    /// </summary>
    public class AliveActivity : Activity
    {
        private static readonly string TAG = "AliveActivity";
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Log.Debug(TAG, "AliveActivity启动");

            Window window = this.Window;
            window.SetGravity(GravityFlags.Start | GravityFlags.Top);
            WindowManagerLayoutParams @params = window.Attributes;

            //宽高
            @params.Width = 1;
            @params.Height = 1;
            //设置位置
            @params.X = 0;
            @params.Y = 0;
            window.Attributes = @params;

            KeepManager.GetInstance().SetKeep(this);
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            Log.Debug(TAG, "AliveActivity关闭");
        }
    }

    /// <summary>
    /// 息屏广播监听
    /// </summary>
    public class KeepAliveReceiver : BroadcastReceiver
    {
        private static readonly string TAG = "KeepAliveReceiver";
        public override void OnReceive(Context context, Intent intent)
        {
            String action = intent.Action;
            Log.Debug(TAG, "onReceive:" + action);
            if (TextUtils.Equals(action, Intent.ActionScreenOff))
            {
                //息屏 开启
                KeepManager.GetInstance().StartKeep(context);
            }
            else if (TextUtils.Equals(action, Intent.ActionScreenOn))
            {
                //开屏 关闭
                KeepManager.GetInstance().FinishKeep();
            }

        }
    }

    /// <summary>
    ///  1像素activity保活管理类
    /// </summary>
    public class KeepManager
    {
        private static readonly KeepManager mInstance = new KeepManager();

        private KeepAliveReceiver mKeepAliveReceiver;

        private WeakReference<Activity> mKeepActivity;

        public KeepManager()
        {

        }

        public static KeepManager GetInstance()
        {
            return mInstance;
        }

        /// <summary>
        /// 注册 开屏 关屏 广播
        /// </summary>
        /// <param name="context"></param>
        public void RegisterKeep(Context context)
        {
            IntentFilter filter = new IntentFilter();

            filter.AddAction(Intent.ActionScreenOn);
            filter.AddAction(Intent.ActionScreenOff);

            mKeepAliveReceiver = new KeepAliveReceiver();
            context.RegisterReceiver(mKeepAliveReceiver, filter);
        }

        /// <summary>
        /// 注销 广播接收者
        /// </summary>
        /// <param name="context"></param>
        public void UnregisterKeep(Context context)
        {
            if (mKeepAliveReceiver != null)
            {
                context.UnregisterReceiver(mKeepAliveReceiver);
            }
        }

        /// <summary>
        /// 开启1像素Activity
        /// </summary>
        /// <param name="context"></param>
        public void StartKeep(Context context)
        {
            Intent intent = new Intent(context, typeof(AliveActivity));
            // 结合 taskAffinity 一起使用 在指定栈中创建这个activity
            intent.SetFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }

        /// <summary>
        /// 关闭1像素Activity
        /// </summary>
        public void FinishKeep()
        {
            if (mKeepActivity != null)
            {
                mKeepActivity.TryGetTarget(out Activity activity);
                if (activity != null)
                {
                    activity.Finish();
                }
                mKeepActivity = null;
            }
        }

        /// <summary>
        /// 设置弱引用
        /// </summary>
        /// <param name="keep"></param>
        public void SetKeep(AliveActivity keep)
        {
            mKeepActivity = new WeakReference<Activity>(keep);
        }
    }

    #endregion
}
