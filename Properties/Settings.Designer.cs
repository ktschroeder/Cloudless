﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Cloudless.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.11.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("BestFit")]
        public string DisplayMode {
            get {
                return ((string)(this["DisplayMode"]));
            }
            set {
                this["DisplayMode"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle {
            get {
                return ((bool)(this["ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle"]));
            }
            set {
                this["ForAutoWindowSizingLeaveSpaceAroundBoundsIfNearScreenSizeAndToggle"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10")]
        public int PixelsSpaceAroundBounds {
            get {
                return ((int)(this["PixelsSpaceAroundBounds"]));
            }
            set {
                this["PixelsSpaceAroundBounds"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ResizeWindowToNewImageWhenOpeningThroughApp {
            get {
                return ((bool)(this["ResizeWindowToNewImageWhenOpeningThroughApp"]));
            }
            set {
                this["ResizeWindowToNewImageWhenOpeningThroughApp"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool BorderOnMainWindow {
            get {
                return ((bool)(this["BorderOnMainWindow"]));
            }
            set {
                this["BorderOnMainWindow"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool LoopGifs {
            get {
                return ((bool)(this["LoopGifs"]));
            }
            set {
                this["LoopGifs"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AlwaysOnTopByDefault {
            get {
                return ((bool)(this["AlwaysOnTopByDefault"]));
            }
            set {
                this["AlwaysOnTopByDefault"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::System.Collections.Specialized.StringCollection RecentFiles {
            get {
                return ((global::System.Collections.Specialized.StringCollection)(this["RecentFiles"]));
            }
            set {
                this["RecentFiles"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool MuteMessages {
            get {
                return ((bool)(this["MuteMessages"]));
            }
            set {
                this["MuteMessages"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10")]
        public double MaxCompressedCopySizeMB {
            get {
                return ((double)(this["MaxCompressedCopySizeMB"]));
            }
            set {
                this["MaxCompressedCopySizeMB"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("black")]
        public string Background {
            get {
                return ((string)(this["Background"]));
            }
            set {
                this["Background"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("FileNameAscending")]
        public string ImageDirectorySortOrder {
            get {
                return ((string)(this["ImageDirectorySortOrder"]));
            }
            set {
                this["ImageDirectorySortOrder"] = value;
            }
        }
    }
}
