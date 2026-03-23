using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Gaming.XboxGameBar;

namespace LaunchPad.Widget;

sealed partial class App : Application
{
    private XboxGameBarWidget? _widget;
    private AppServiceConnection? _companionConnection;
    private BackgroundTaskDeferral? _appServiceDeferral;

    public static AppServiceConnection? CompanionConnection { get; private set; }

    public App()
    {
        this.InitializeComponent();
        this.Suspending += OnSuspending;
    }

    protected override void OnActivated(IActivatedEventArgs args)
    {
        if (args.Kind == ActivationKind.Protocol)
        {
            var protocolArgs = args as IProtocolActivatedEventArgs;
            if (protocolArgs?.Uri.SchemeName == "ms-gamebarwidget")
            {
                var widgetArgs = args as XboxGameBarWidgetActivatedEventArgs;
                if (widgetArgs != null && widgetArgs.IsLaunchActivation)
                {
                    var rootFrame = new Frame();
                    Window.Current.Content = rootFrame;

                    _widget = new XboxGameBarWidget(
                        widgetArgs,
                        Window.Current.CoreWindow,
                        rootFrame);

                    rootFrame.Navigate(typeof(LaunchPadWidget));
                    Window.Current.Activate();
                }
            }
        }
    }

    protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
    {
        base.OnBackgroundActivated(args);

        if (args.TaskInstance.TriggerDetails is AppServiceTriggerDetails details)
        {
            _appServiceDeferral = args.TaskInstance.GetDeferral();
            _companionConnection = details.AppServiceConnection;
            CompanionConnection = _companionConnection;

            args.TaskInstance.Canceled += (_, _) =>
            {
                _appServiceDeferral?.Complete();
                CompanionConnection = null;
            };
        }
    }

    private void OnSuspending(object sender, SuspendingEventArgs e)
    {
        var deferral = e.SuspendingOperation.GetDeferral();
        _widget = null;
        CompanionConnection = null;
        deferral.Complete();
    }
}
