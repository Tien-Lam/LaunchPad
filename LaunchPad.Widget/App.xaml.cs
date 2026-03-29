using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Gaming.XboxGameBar;
using LaunchPad.Widget.Services;

namespace LaunchPad.Widget;

sealed partial class App : Application
{
    private XboxGameBarWidget? _widget;
    private AppServiceConnection? _companionConnection;
    private BackgroundTaskDeferral? _appServiceDeferral;

    public static AppServiceConnection? CompanionConnection { get; private set; }
    public static XboxGameBarWidget? Widget { get; private set; }

    public App()
    {
        this.InitializeComponent();
        this.Suspending += OnSuspending;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Widget only works via Game Bar protocol activation. Close if launched directly.
        Current.Exit();
    }

    protected override void OnActivated(IActivatedEventArgs args)
    {
        WidgetLog.Write($"OnActivated: Kind={args.Kind}");

        if (args.Kind == ActivationKind.Protocol)
        {
            var protocolArgs = args as IProtocolActivatedEventArgs;
            WidgetLog.Write($"OnActivated: Scheme={protocolArgs?.Uri.Scheme}, Uri={protocolArgs?.Uri}");

            if (protocolArgs?.Uri.Scheme == "ms-gamebarwidget")
            {
                var widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                WidgetLog.Write($"OnActivated: IsLaunchActivation={widgetArgs?.IsLaunchActivation}, AppExtensionId={widgetArgs?.AppExtensionId}");

                if (widgetArgs != null)
                {
                    var rootFrame = new Frame();
                    Window.Current.Content = rootFrame;

                    _widget = new XboxGameBarWidget(
                        widgetArgs,
                        Window.Current.CoreWindow,
                        rootFrame);
                    Widget = _widget;

                    rootFrame.Navigate(typeof(LaunchPadWidget));
                    Window.Current.Activate();
                    WidgetLog.Write("OnActivated: Widget created and page navigated");
                }
            }
        }
    }

    protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
    {
        base.OnBackgroundActivated(args);
        WidgetLog.Write($"OnBackgroundActivated: IsAppService={args.TaskInstance.TriggerDetails is AppServiceTriggerDetails}");

        if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails details)
        {
            _appServiceDeferral = args.TaskInstance.GetDeferral();
            _companionConnection = details.AppServiceConnection;
            CompanionConnection = _companionConnection;
            WidgetLog.Write("OnBackgroundActivated: CompanionConnection set");

            _companionConnection.RequestReceived += Services.CompanionClient.OnCompanionMessage;
            _companionConnection.ServiceClosed += (_, _) =>
            {
                WidgetLog.Write("ServiceClosed fired");
                CompanionConnection = null;
                TryRelaunchCompanion();
            };

            args.TaskInstance.Canceled += (_, _) =>
            {
                WidgetLog.Write("BackgroundTask Canceled fired");
                // Dispose the connection to trigger ServiceClosed on the companion side,
                // so it releases the mutex and exits instead of becoming a zombie
                try { _companionConnection?.Dispose(); }
                catch { }
                _companionConnection = null;
                CompanionConnection = null;
                _appServiceDeferral?.Complete();
            };
        }
    }

    private async void TryRelaunchCompanion()
    {
        int[] delays = { 1000, 2000, 4000 };
        foreach (var delay in delays)
        {
            await Task.Delay(delay);
            if (CompanionConnection != null) return;
            try
            {
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            }
            catch { }
        }
    }

    private void OnSuspending(object sender, SuspendingEventArgs e)
    {
        var deferral = e.SuspendingOperation.GetDeferral();
        _widget = null;
        Widget = null;
        CompanionConnection = null;
        deferral.Complete();
    }
}
