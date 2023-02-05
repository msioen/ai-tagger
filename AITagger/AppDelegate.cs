using AppKit;
using Foundation;
using ObjCRuntime;

namespace AITagger
{
    [Register ("AppDelegate")]
    public class AppDelegate : NSApplicationDelegate
    {
        NSStatusItem _statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Square);

        public AppDelegate ()
        {
        }

        public override void DidFinishLaunching (NSNotification notification)
        {
            // setup menu
            _statusItem.Button.Image = NSImage.ImageNamed("StatusBarIcon");

            var menu = new NSMenu();

            //var scanSelectItem = new NSMenuItem("Scan - select", OnScanSelect);
            //scanSelectItem.BindHotKey(NSUserDefaultsController.SharedUserDefaultsController, Constants.KEY_HOTKEY_SCAN_SELECT);
            //menu.AddItem(scanSelectItem);

            //var scanClickItem = new NSMenuItem("Scan - click", OnScanClick);
            //scanClickItem.BindHotKey(NSUserDefaultsController.SharedUserDefaultsController, Constants.KEY_HOTKEY_SCAN_CLICK);
            //menu.AddItem(scanClickItem);

            //menu.AddItem(NSMenuItem.SeparatorItem);
            //menu.AddItem(new NSMenuItem("Preferences", OnPreferences));
            //menu.AddItem(new NSMenuItem("Check for updates", OnCheckForUpdates));
            //menu.AddItem(new NSMenuItem("Quit", OnQuit));
            _statusItem.Menu = menu;
        }

        public override void WillTerminate (NSNotification notification)
        {
        }
    }
}

