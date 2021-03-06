﻿using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace UDDNSQuery {
    /*
     * Based on http://archive.msdn.microsoft.com/WindowsAPICodePack and removed everything not needed by this very
     * specific use case. This class is not designed to be used for anything other than StatusDialog.cs.
     */

    /// <summary>
    /// Displays a TaskDialog through user32.dll P/Invoke.
    /// </summary>
    public class TaskDialog : IDisposable {
        internal const int UserMessage = 0x0400; // Various important window messages.
        private int selectedButtonId;
        private int progressBarMinimum;
        private int progressBarMaximum;
        private int progressBarValue;
        private string caption; // Dialog title.
        private string instructionText; // Blue heading text.
        private string text; // Dialog body text.
        private TaskDialogStandardIcon icon;
        private IntPtr[] updatedStrings;
        private IntPtr hWndDialog;
        private IntPtr hWndOwner;
        private TaskDialogShowState showState;
        private TaskDialogResult result;
        private TaskDialogProgressBarState progressBarState;
        private ITaskbarList4 taskbarList;

        /// <summary>
        /// Raised when dialog is opened/shown.
        /// </summary>
        public event EventHandler Opened;
        /// <summary>
        /// Raised when dialog is closing.
        /// </summary>
        public event EventHandler<CancelEventArgs> Closing;
        /// <summary>
        /// Raised when a hyperlink in the dialog is clicked.
        /// </summary>
        public event EventHandler<HyperlinkEventArgs> HyperlinkClick;
        /// <summary>
        /// Dialog window title.
        /// </summary>
        /// <exception cref="System.NotSupportedException"></exception>
        public string Caption { get { return caption; } set { if ( NativeDialogShowing ) throw new NotSupportedException(); caption = value; } }
        /// <summary>
        /// Dialog instruction text (blue "heading" text).
        /// </summary>
        public string InstructionText { get { return instructionText; } set { instructionText = value; if ( NativeDialogShowing ) UpdateInstruction( value ); } }
        /// <summary>
        /// Regular text in the dialog.
        /// </summary>
        public string Text { get { return text; } set { text = value; if ( NativeDialogShowing ) { UpdateText( value ); } } }
        /// <summary>
        /// Gets or sets the icon in the dialog.
        /// </summary>
        public TaskDialogStandardIcon Icon { get { return icon; } set { icon = value; if ( NativeDialogShowing ) { UpdateMainIcon( value ); } } }
        /// <summary>
        /// Gets or sets the progress bar range (minimum, maximum).
        /// </summary>
        public int[] ProgressBarRange { get { return new int[2] { progressBarMinimum, progressBarMaximum }; } set { UpdateProgressBarRange( value ); progressBarMinimum = value[0]; ; progressBarMaximum = value[1]; } }
        /// <summary>
        /// Gets or sets the progress bar current value.
        /// </summary>
        public int ProgressBarValue { get { return progressBarValue; } set { UpdateProgressBarValue( value ); progressBarValue = value; } }
        /// <summary>
        /// Gets a value indicating whether the dialog is showing.
        /// </summary>
        /// <value><c>true</c> if [native dialog showing]; otherwise, <c>false</c>.</value>
        public bool NativeDialogShowing { get { return (showState == TaskDialogShowState.Showing || showState == TaskDialogShowState.Closing); } }
        /// <summary>
        /// Gets a value indicating whether taskbar progress bars are supported on the platform (Windows 7 and up).
        /// </summary>
        /// <value><c>true</c> if [supports taskbar progress]; otherwise, <c>false</c>.</value>
        public static bool SupportsTaskbarProgress { get { return (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.CompareTo( new Version( 6, 1 ) ) >= 0); } }
        /// <summary>
        /// Gets the TaskDialog result.
        /// </summary>
        public TaskDialogResult Result { get { return result; } }
        /// <summary>
        /// Gets or sets the state of the progress bar.
        /// </summary>
        public TaskDialogProgressBarState ProgressBarState { get { return progressBarState; } set { progressBarState = value; UpdateProgressBarState( progressBarState ); } }

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskDialog"/> class.
        /// </summary>
        public TaskDialog() {
            progressBarMinimum = 0;
            progressBarMaximum = 100;
            progressBarValue = 0;
            updatedStrings = new IntPtr[Enum.GetNames( typeof( TaskDialogElement ) ).Length];
            hWndOwner = FindWindow( "MsiDialogCloseClass", 0 );
            if ( SupportsTaskbarProgress ) taskbarList = (ITaskbarList4) new CTaskbarList();
        }

        #region Enums

        /// <summary>
        /// HRESULT Wrapper    
        /// </summary>    
        public enum HResult {
            /// <summary>
            /// Result OK.
            /// </summary>
            Ok = 0x0000,
            /// <summary>
            /// Result false.
            /// </summary>
            False = 0x0001
        }

        /// <summary>
        /// Current showing state of the TaskDialog.
        /// </summary>
        public enum TaskDialogShowState {
            /// <summary>
            /// Dialog isn't showing yet.
            /// </summary>
            PreShow,
            /// <summary>
            /// Dialog currently showing.
            /// </summary>
            Showing,
            /// <summary>
            /// Dialog is about to close.
            /// </summary>
            Closing,
            /// <summary>
            /// Dialog has closed and is no longer displayed.
            /// </summary>
            Closed
        }

        /// <summary>
        /// Standard icon to display in the dialog.
        /// </summary>
        public enum TaskDialogStandardIcon {
            /// <summary>
            /// No icon.
            /// </summary>
            None = 0,
            /// <summary>
            /// Red error icon.
            /// </summary>
            Error = 65534,
            /// <summary>
            /// Blue information icon.
            /// </summary>
            Information = 65533
        }

        /// <summary>
        /// Where to display the icon.
        /// </summary>
        public enum TaskDialogIconElement {
            /// <summary>
            /// Icon to display in the dialog next to InstructionText and Text.
            /// </summary>
            Main,
            /// <summary>
            /// Icon to display in the footer below the standard buttons.
            /// </summary>
            Footer
        }

        /// <summary>
        /// Dialog progress bar state.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue" )]
        public enum TaskDialogProgressBarState {
            /// <summary>
            /// Normal green progress bar.
            /// </summary>
            Normal = 0x0001,
            /// <summary>
            /// Red progress bar.
            /// </summary>
            Error = 0x0002
        }

        /// <summary>
        /// Represents the thumbnail progress bar state.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1027:MarkEnumsWithFlags" ), System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue" )]
        public enum TaskbarProgressBarState {
            /// <summary>
            /// Normal green progress bar.
            /// </summary>
            Normal = 0x2,
            /// <summary>
            /// Red progress bar.
            /// </summary>
            Error = 0x4
        }

        /// <summary>
        /// Task Dialog - flags
        /// </summary>
        [Flags]
        public enum TaskDialogOptions {
            /// <summary>
            /// Enable dialog hyperlink support.
            /// </summary>
            EnableHyperlinks = 0x0001,
            /// <summary>
            /// Allow dialog to be canceled.
            /// </summary>
            AllowCancel = 0x0008,
            /// <summary>
            /// Show a progress bar in the dialog.
            /// </summary>
            ShowProgressBar = 0x0200,
            /// <summary>
            /// Position the dialog over the parent window.
            /// </summary>
            PositionRelativeToWindow = 0x1000
        }

        /// <summary>
        /// Which text in the dialog to update.
        /// </summary>
        public enum TaskDialogElement {
            /// <summary>
            /// Dialog regular text.
            /// </summary>
            Content,
            /// <summary>
            /// Expanded area text.
            /// </summary>
            ExpandedInformation,
            /// <summary>
            /// Footer area text.
            /// </summary>
            Footer,
            /// <summary>
            /// Dialog instruction text (blue "heading" text).
            /// </summary>
            MainInstruction
        }

        /// <summary>
        /// Task Dialog event notifications from the DLL.
        /// </summary>
        public enum TaskDialogNotification {
            /// <summary>
            /// Dialog was created.
            /// </summary>
            Created = 0,
            /// <summary>
            /// A button was clicked in the dialog.
            /// </summary>
            ButtonClicked = 2,
            /// <summary>
            /// A link was clicked in the dialog.
            /// </summary>
            HyperlinkClicked = 3,
            /// <summary>
            /// The dialog has been destroyed (unmanaged resources freed).
            /// </summary>
            Destroyed = 5
        }

        /// <summary>
        /// Indicates the various buttons and options clicked by the user on the task dialog and identifies common buttons.
        /// </summary>        
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1027:MarkEnumsWithFlags" ), System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue" )]
        public enum TaskDialogResult {
            /// <summary>
            /// The ok button.
            /// </summary>
            Ok = 0x0001,
            /// <summary>
            /// The yes button.
            /// </summary>
            Yes = 0x0002,
            /// <summary>
            /// The no button.
            /// </summary>
            No = 0x0004,
            /// <summary>
            /// The cancel button.
            /// </summary>
            Cancel = 0x0008
        }

        /// <summary>
        /// Identify button *return values* - note that, unfortunately, these are different from the inbound button values.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1027:MarkEnumsWithFlags" ), System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue" )]
        public enum TaskDialogCommonButtonReturnId {
            /// <summary>
            /// The ok button.
            /// </summary>
            Ok = 1,
            /// <summary>
            /// The cancel button.
            /// </summary>
            Cancel = 2,
            /// <summary>
            /// The yes button.
            /// </summary>
            Yes = 6,
            /// <summary>
            /// The no button.
            /// </summary>
            No = 7,
            /// <summary>
            /// The close button.
            /// </summary>
            Close = 8
        }

        /// <summary>
        /// Send message to the DLL.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue" )]
        public enum TaskDialogMessage {
            /// <summary>
            /// Programmatically click a button.
            /// </summary>
            ClickButton = UserMessage + 102, // wParam = Button ID
            /// <summary>
            /// The set progress bar state.
            /// </summary>
            SetProgressBarState = UserMessage + 104, // wParam = new progress state
            /// <summary>
            /// The set progress bar range.
            /// </summary>
            SetProgressBarRange = UserMessage + 105, // lParam = MAKELPARAM(nMinRange, nMaxRange)
            /// <summary>
            /// The set progress bar position in the dialog.
            /// </summary>
            SetProgressBarPosition = UserMessage + 106, // wParam = new position
            /// <summary>
            /// The set element text (caption/maininstruction/content).
            /// </summary>
            SetElementText = UserMessage + 108, // wParam = element (TASKDIALOG_ELEMENTS), lParam = new element text (LPCWSTR)
            /// <summary>
            /// Enable/disable a button.
            /// </summary>
            EnableButton = UserMessage + 111, // lParam = 0 (disable), lParam != 0 (enable), wParam = Button ID
            /// <summary>
            /// Change the icon in the dialog.
            /// </summary>
            UpdateIcon = UserMessage + 116  // wParam = icon element (TASKDIALOG_ICON_ELEMENTS), lParam = new icon (hIcon if TDF_USE_HICON_* was set, PCWSTR otherwise)
        }

        #endregion

        #region Core

        /// <summary>
        /// The main function that actually creates Task Dialogs using calls to comctl32.dll.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass" ), System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Interoperability", "CA1400:PInvokeEntryPointsShouldExist" ), DllImport( "Comctl32.dll", SetLastError = true )]
        private static extern HResult TaskDialogIndirect(
            [In] TaskDialogConfiguration taskConfig,
            [Out] out int button,
            [Out] IntPtr radioButton,
            [Out] IntPtr verificationFlagChecked );

        /// <summary>
        /// Sends the specified message to a window or windows. The SendMessage function calls 
        /// the window procedure for the specified window and does not return until the window 
        /// procedure has processed the message. 
        /// </summary>
        /// <param name="windowHandle">Handle to the window whose window procedure will receive the message. 
        /// If this parameter is HWND_BROADCAST, the message is sent to all top-level windows in the system, 
        /// including disabled or invisible unowned windows, overlapped windows, and pop-up windows; 
        /// but the message is not sent to child windows.
        /// </param>
        /// <param name="message">Specifies the message to be sent.</param>
        /// <param name="wparam">Specifies additional message-specific information.</param>
        /// <param name="lparam">Specifies additional message-specific information.</param>
        /// <returns>A return code specific to the message being sent.</returns>        
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass" ), DllImport( "user32.dll", CharSet = CharSet.Auto, SetLastError = true )]
        private static extern IntPtr SendMessage(
            IntPtr windowHandle,
            uint message,
            IntPtr wparam,
            IntPtr lparam
        );

        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", MessageId = "1" ), System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass" ), DllImport( "user32.dll", CharSet = CharSet.Unicode )]
        internal static extern IntPtr FindWindow( string strClassName, int nptWindowName );

        // Main task dialog configuration struct.
        // NOTE: Packing must be set to 4 to make this work on 64-bit platforms.
        [StructLayout( LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4 )]
        private class TaskDialogConfiguration {
            internal uint size;
            internal IntPtr parentHandle;
            internal IntPtr instance;
            internal TaskDialogOptions taskDialogFlags;
            internal TaskDialogResult commonButtons;
            [MarshalAs( UnmanagedType.LPWStr )]
            internal string windowTitle;
            internal IconUnion mainIcon; // NOTE: 32-bit union field, holds pszMainIcon as well
            [MarshalAs( UnmanagedType.LPWStr )]
            internal string mainInstruction;
            [MarshalAs( UnmanagedType.LPWStr )]
            internal string content;
            internal uint buttonCount;
            internal IntPtr buttons;           // Ptr to TASKDIALOG_BUTTON structs
            internal int defaultButtonIndex;
            internal uint radioButtonCount;
            internal IntPtr radioButtons;      // Ptr to TASKDIALOG_BUTTON structs
            internal int defaultRadioButtonIndex;
            [MarshalAs( UnmanagedType.LPWStr )]
            internal string verificationText;
            [MarshalAs( UnmanagedType.LPWStr )]
            internal string expandedInformation;
            [MarshalAs( UnmanagedType.LPWStr )]
            internal string expandedControlText;
            [MarshalAs( UnmanagedType.LPWStr )]
            internal string collapsedControlText;
            internal IconUnion footerIcon;  // NOTE: 32-bit union field, holds pszFooterIcon as well
            [MarshalAs( UnmanagedType.LPWStr )]
            internal string footerText;
            internal TaskDialogCallback callback;
            internal IntPtr callbackData;
            internal uint width;
        }

        // NOTE: We include a "spacer" so that the struct size varies on 
        // 64-bit architectures.
        [StructLayout( LayoutKind.Explicit, CharSet = CharSet.Auto )]
        private struct IconUnion {
            private IconUnion( int i ) {
                mainIcon = i;
                spacer = IntPtr.Zero;
            }

            [FieldOffset( 0 )]
            private int mainIcon;

            // This field is used to adjust the length of the structure on 32/64bit OS.
            [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields" )]
            [FieldOffset( 0 )]
            private IntPtr spacer;

            /// <summary>
            /// Gets the handle to the Icon
            /// </summary>
            public int MainIcon { get { return mainIcon; } }
        }

        private int SendMessageHelper( TaskDialogMessage message, int wparam, long lparam ) {
            return (int) SendMessage( hWndDialog, (uint) message, (IntPtr) wparam, new IntPtr( lparam ) );
        }

        [ComImportAttribute()]
        [GuidAttribute( "c43dc798-95d1-4bea-9030-bb99e2983a1a" )]
        [InterfaceTypeAttribute( ComInterfaceType.InterfaceIsIUnknown )]
        private interface ITaskbarList4 {
            // ITaskbarList
            [PreserveSig]
            void HrInit();
            [PreserveSig]
            void AddTab( IntPtr hwnd );
            [PreserveSig]
            void DeleteTab( IntPtr hwnd );
            [PreserveSig]
            void ActivateTab( IntPtr hwnd );
            [PreserveSig]
            void SetActiveAlt( IntPtr hwnd );

            // ITaskbarList2
            [PreserveSig]
            void MarkFullscreenWindow(
                IntPtr hwnd,
                [MarshalAs( UnmanagedType.Bool )] bool fFullscreen );

            // ITaskbarList3
            [PreserveSig]
            void SetProgressValue( IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal );
            [PreserveSig]
            void SetProgressState( IntPtr hwnd, TaskbarProgressBarState tbpFlags );
        }

        [GuidAttribute( "56FDF344-FD6D-11d0-958A-006097C9A090" )]
        [ClassInterfaceAttribute( ClassInterfaceType.None )]
        [ComImportAttribute()]
        private class CTaskbarList { }

        #endregion

        #region Callbacks and Events

        // Task Dialog config and related structs (for TaskDialogIndirect())
        private delegate int TaskDialogCallback( IntPtr hwnd, uint message, IntPtr wparam, IntPtr lparam,
            IntPtr referenceData );

        private int DialogProc( IntPtr windowHandle, uint message, IntPtr wparam, IntPtr lparam, IntPtr referenceData ) {
            // Fetch the HWND - it may be the first time we're getting it.
            hWndDialog = windowHandle;

            // Big switch on the various notifications the 
            // dialog proc can get.
            switch ( (TaskDialogNotification) message ) {
                case TaskDialogNotification.Created:
                    UpdateMainIcon( icon );
                    UpdateProgressBarRange( new int[2] { progressBarMinimum, progressBarMaximum } );
                    UpdateProgressBarState( progressBarState );
                    UpdateProgressBarValue( progressBarValue );
                    UpdateProgressBarValue( progressBarValue );
                    RaiseOpenedEvent();
                    break;
                case TaskDialogNotification.ButtonClicked:
                    if ( (int) wparam < (int) TaskDialogCommonButtonReturnId.Close + 1 )
                        return RaiseClosingEvent();
                    return (int) HResult.False;
                case TaskDialogNotification.HyperlinkClicked:
                    RaiseHyperlinkClickEvent( Marshal.PtrToStringUni( lparam ) );
                    break;
                default:
                    break;
            }
            return (int) HResult.Ok;
        }

        // Gives event subscriber a chance to prevent 
        // the dialog from closing, based on 
        // the current state of the app and the button 
        // used to commit. Note that we don't 
        // have full access at this stage to 
        // the full dialog state.
        private int RaiseClosingEvent() {
            EventHandler<CancelEventArgs> handler = Closing;
            if ( handler != null ) {
                CancelEventArgs e = new CancelEventArgs();

                // Raise the event and determine how to proceed.
                handler( this, e );
                if ( e.Cancel ) { return (int) HResult.False; }
            }

            // It's okay to let the dialog close.
            return (int) HResult.Ok;
        }

        private void RaiseOpenedEvent() {
            if ( Opened != null ) {
                Opened( this, EventArgs.Empty );
            }
        }

        private void RaiseHyperlinkClickEvent( string link ) {
            EventHandler<HyperlinkEventArgs> handler = HyperlinkClick;
            if ( handler != null ) {
                handler( this, new HyperlinkEventArgs( link ) );
            }
        }

        /// <summary>
        /// Defines event data associated with a HyperlinkClick event.
        /// </summary>
        public class HyperlinkEventArgs : EventArgs {
            /// <summary>
            /// Creates a new instance of this class with the specified link text.
            /// </summary>
            /// <param name="linkText">The text of the hyperlink that was clicked.</param>
            public HyperlinkEventArgs( string linkText ) {
                LinkText = linkText;
            }

            /// <summary>
            /// Gets or sets the text of the hyperlink that was clicked.
            /// </summary>
            public string LinkText { get; set; }
        }

        #endregion

        #region Update Members

        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly" )]
        private void UpdateProgressBarValue( int i ) {
            if ( i < progressBarMinimum || i > progressBarMaximum ) throw new ArgumentOutOfRangeException();
            SendMessageHelper( TaskDialogMessage.SetProgressBarPosition, i, 0 );
            if ( !SupportsTaskbarProgress ) return;
            taskbarList.SetProgressValue( hWndOwner, Convert.ToUInt32( i ), Convert.ToUInt32( progressBarMaximum ) );
        }

        private void UpdateProgressBarState( TaskDialogProgressBarState state ) {
            SendMessageHelper( TaskDialogMessage.SetProgressBarState, (int) state, 0 );
            if ( !SupportsTaskbarProgress ) return;
            TaskbarProgressBarState taskbarState = TaskbarProgressBarState.Normal;
            if ( state == TaskDialogProgressBarState.Error ) taskbarState = TaskbarProgressBarState.Error;
            taskbarList.SetProgressState( hWndOwner, taskbarState );
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Usage", "CA2208:InstantiateArgumentExceptionsCorrectly" )]
        private void UpdateProgressBarRange( int[] range ) {
            int min = range[0];
            int max = range[1];
            if ( min < 0 || min >= max || max <= min ) throw new ArgumentOutOfRangeException();
            SendMessageHelper( TaskDialogMessage.SetProgressBarRange, 0, (long) (max << 16) + min );
        }

        private void UpdateTextCore( string s, TaskDialogElement element ) {
            // FreeOldString
            int elementIndex = (int) element;
            if ( updatedStrings[elementIndex] != IntPtr.Zero ) {
                Marshal.FreeHGlobal( updatedStrings[elementIndex] );
                updatedStrings[elementIndex] = IntPtr.Zero;
            }

            // MakeNewString
            IntPtr newStringPtr = Marshal.StringToHGlobalUni( s );
            updatedStrings[(int) element] = newStringPtr;
            SendMessageHelper( TaskDialogMessage.SetElementText, (int) element, (long) newStringPtr );
        }

        private void UpdateText( string s ) {
            UpdateTextCore( s, TaskDialogElement.Content );
        }

        private void UpdateInstruction( string s ) {
            UpdateTextCore( s, TaskDialogElement.MainInstruction );
        }

        private void UpdateMainIcon( TaskDialogStandardIcon icon ) {
            TaskDialogIconElement element = TaskDialogIconElement.Main;
            SendMessageHelper( TaskDialogMessage.UpdateIcon, (int) element, (long) icon );
        }

        /// <summary>
        /// Enables/disables a button in the dialog.
        /// </summary>
        /// <param name="buttonID">The button ID.</param>
        /// <param name="enabled">if set to <c>true</c> [enabled].</param>
        public void UpdateButtonEnabled( TaskDialogCommonButtonReturnId buttonID, bool enabled ) {
            SendMessageHelper( TaskDialogMessage.EnableButton, (int) buttonID, enabled == true ? 1 : 0 );
        }

        #endregion

        #region Show and Close

        /// <summary>
        /// Displays a cancellation TaskDialog asking the user if they're sure they want to cancel their task.
        /// </summary>
        /// <param name="title">The dialog's title.</param>
        /// <param name="text">The dialog's body.</param>
        /// <returns>TaskDialogResult.</returns>
        public TaskDialogResult ShowCancellation( string title, string text ) {
            result = TaskDialogResult.No;
            TaskDialogConfiguration nativeConfig = new TaskDialogConfiguration();
            Icon = TaskDialogStandardIcon.Information;

            nativeConfig.size = (uint) Marshal.SizeOf( nativeConfig );
            nativeConfig.parentHandle = hWndOwner;
            nativeConfig.commonButtons = TaskDialogResult.Yes | TaskDialogResult.No;
            nativeConfig.content = text;
            nativeConfig.windowTitle = title;
            nativeConfig.mainInstruction = instructionText;
            nativeConfig.taskDialogFlags = TaskDialogOptions.AllowCancel | TaskDialogOptions.PositionRelativeToWindow;
            nativeConfig.callback = new TaskDialogCallback( DialogProc );

            showState = TaskDialogShowState.Showing;
            using ( new EnableThemingInScope( true ) ) { // Here is the way we use "vanilla" P/Invoke to call TaskDialogIndirect().
                TaskDialogIndirect(
                    nativeConfig,
                    out selectedButtonId,
                    IntPtr.Zero,
                    IntPtr.Zero );
            }
            showState = TaskDialogShowState.Closed;

            if ( (TaskDialogCommonButtonReturnId) selectedButtonId == TaskDialogCommonButtonReturnId.Yes ) {
                result = TaskDialogResult.Yes;
            }

            // Free up strings.
            if ( updatedStrings != null ) {
                for ( int i = 0; i < updatedStrings.Length; i++ ) {
                    if ( updatedStrings[i] != IntPtr.Zero ) {
                        Marshal.FreeHGlobal( updatedStrings[i] );
                        updatedStrings[i] = IntPtr.Zero;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Displays a TaskDialog-based error dialog with the error icon.
        /// </summary>
        /// <param name="title">The dialog's title.</param>
        /// <param name="text">The dialog's body.</param>
        /// <returns>TaskDialogResult.</returns>
        public TaskDialogResult ShowError( string title, string text ) {
            result = TaskDialogResult.No;
            TaskDialogConfiguration nativeConfig = new TaskDialogConfiguration();
            Icon = TaskDialogStandardIcon.Error;

            nativeConfig.size = (uint) Marshal.SizeOf( nativeConfig );
            nativeConfig.parentHandle = hWndOwner;
            nativeConfig.commonButtons = TaskDialogResult.Ok;
            nativeConfig.content = text;
            nativeConfig.windowTitle = title;
            nativeConfig.mainInstruction = instructionText;
            nativeConfig.taskDialogFlags = TaskDialogOptions.AllowCancel | TaskDialogOptions.PositionRelativeToWindow;
            nativeConfig.callback = new TaskDialogCallback( DialogProc );

            showState = TaskDialogShowState.Showing;
            using ( new EnableThemingInScope( true ) ) { // Here is the way we use "vanilla" P/Invoke to call TaskDialogIndirect().
                TaskDialogIndirect(
                    nativeConfig,
                    out selectedButtonId,
                    IntPtr.Zero,
                    IntPtr.Zero );
            }
            showState = TaskDialogShowState.Closed;

            if ( (TaskDialogCommonButtonReturnId) selectedButtonId == TaskDialogCommonButtonReturnId.Ok ) {
                result = TaskDialogResult.Ok;
            }

            // Free up strings.
            if ( updatedStrings != null ) {
                for ( int i = 0; i < updatedStrings.Length; i++ ) {
                    if ( updatedStrings[i] != IntPtr.Zero ) {
                        Marshal.FreeHGlobal( updatedStrings[i] );
                        updatedStrings[i] = IntPtr.Zero;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Shows the TaskDialog.
        /// </summary>
        /// <returns>TaskDialogResult.</returns>
        public TaskDialogResult Show() {
            result = TaskDialogResult.Cancel;
            TaskDialogConfiguration nativeConfig = new TaskDialogConfiguration();

            nativeConfig.size = (uint) Marshal.SizeOf( nativeConfig );
            nativeConfig.parentHandle = hWndOwner;
            nativeConfig.commonButtons = TaskDialogResult.Cancel;
            nativeConfig.content = text;
            nativeConfig.windowTitle = caption;
            nativeConfig.mainInstruction = instructionText;
            nativeConfig.taskDialogFlags = TaskDialogOptions.AllowCancel | TaskDialogOptions.ShowProgressBar | TaskDialogOptions.PositionRelativeToWindow | TaskDialogOptions.EnableHyperlinks;
            nativeConfig.callback = new TaskDialogCallback( DialogProc );

            // Show the dialog.
            // NOTE: this is a BLOCKING call; the dialog proc callbacks
            // will be executed by the same thread as the 
            // Show() call before the thread of execution 
            // continues to the end of this method.
            showState = TaskDialogShowState.Showing;
            using ( new EnableThemingInScope( true ) ) { // Here is the way we use "vanilla" P/Invoke to call TaskDialogIndirect().
                TaskDialogIndirect(
                    nativeConfig,
                    out selectedButtonId,
                    IntPtr.Zero,
                    IntPtr.Zero );
            }
            showState = TaskDialogShowState.Closed;

            // Build and return dialog result to public API - leaving it
            // null after an exception is thrown is fine in this case
            if ( (TaskDialogCommonButtonReturnId) selectedButtonId == TaskDialogCommonButtonReturnId.Ok ) {
                result = TaskDialogResult.Ok;
            }

            // Reset progress bar.
            ProgressBarState = TaskDialogProgressBarState.Normal;
            ProgressBarValue = progressBarMinimum;

            // Free up strings.
            if ( updatedStrings != null ) {
                for ( int i = 0; i < updatedStrings.Length; i++ ) {
                    if ( updatedStrings[i] != IntPtr.Zero ) {
                        Marshal.FreeHGlobal( updatedStrings[i] );
                        updatedStrings[i] = IntPtr.Zero;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Close TaskDialog with a given TaskDialogResult
        /// </summary>
        /// <param name="result">TaskDialogResult to return from the Show() method</param>
        /// <exception cref="InvalidOperationException">if TaskDialog is not showing.</exception>
        public void Close( TaskDialogResult result ) {
            if ( !NativeDialogShowing ) throw new InvalidOperationException();
            showState = TaskDialogShowState.Closing;
            int id = (int) TaskDialogCommonButtonReturnId.Cancel;
            if ( result == TaskDialogResult.Ok ) id = (int) TaskDialogCommonButtonReturnId.Ok;
            SendMessageHelper( TaskDialogMessage.ClickButton, id, 0 );
        }

        #endregion

        #region Cleanup Code

        // Dispose pattern - cleans up data and structs for 
        // a) any native dialog currently showing, and
        // b) anything else that the outer TaskDialog has.
        private bool disposed;

        /// <summary>
        /// Dispose TaskDialog Resources
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1063:ImplementIDisposableCorrectly" )]
        public virtual void Dispose() {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        /// <summary>
        /// TaskDialog Finalizer
        /// </summary>
        ~TaskDialog() {
            Dispose( false );
        }

        /// <summary>
        /// Dispose TaskDialog Resources
        /// </summary>
        /// <param name="disposing">If true, indicates that this is being called via Dispose rather than via the finalizer.</param>
        internal void Dispose( bool disposing ) {
            if ( disposed ) return;
            disposed = true;
            if ( disposing && showState == TaskDialogShowState.Showing ) Close( TaskDialogResult.Cancel );
        }

        #endregion
    }
}
