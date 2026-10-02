using System.Runtime.InteropServices;

namespace LyricsX.Platform.Windows.Taskbar;

/// <summary>
/// Minimal UI Automation COM interop (UIAutomationCore): element tree walking
/// without any WPF dependency, so WinUI projects can reference this assembly.
/// Slot order follows the official IUIAutomation/IUIAutomationElement vtables;
/// unused members stay declared as void stubs to preserve slot positions.
/// </summary>
internal static class UIAutomationInterop
{
    public const int TreeScope_Descendants = 0x0004;

    // UIA_ControlTypeId subset used for interactive-element filtering
    public const int ControlType_Button = 40000;
    public const int ControlType_List = 40008;
    public const int ControlType_MenuItem = 40011;
    public const int ControlType_ToolBar = 40021;
    public const int ControlType_Custom = 40025;

    public static IUIAutomation Create() =>
        (IUIAutomation)Activator.CreateInstance(typeof(CUIAutomation))!;

    [ComImport]
    [Guid("ff40dba0-8fef-4a02-ae84-fd683d4e2b33")]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class CUIAutomation;

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("30cbe57d-d9d3-4eac-bca9-14e5f1e3d38b")]
    internal interface IUIAutomation
    {
        void CompareElements();
        void CompareRuntimeIds();
        void GetRootElement();
        void GetRootElementBuildCache();

        [PreserveSig]
        int ElementFromHandle(nint hwnd, out IUIAutomationElement element);

        void ElementFromHandleBuildCache();
        void ElementFromPoint();
        void ElementFromPointBuildCache();
        void GetFocusedElement();
        void GetFocusedElementBuildCache();
        void GetNearestElementFromPoint();

        [PreserveSig]
        int CreateTrueCondition(out IUIAutomationCondition condition);

        void CreateFalseCondition();

        [PreserveSig]
        int CreatePropertyCondition(int propertyId, nint value, out IUIAutomationCondition condition);

        void CreatePropertyConditionEx();
        void CreatePropertyConditionFlags();

        [PreserveSig]
        int CreateAndCondition(
            IUIAutomationCondition left,
            IUIAutomationCondition right,
            out IUIAutomationCondition condition);

        void CreateAndConditionFromArray();
        void CreateAndConditionFromNativeArray();
        void CreateOrCondition();
        void CreateOrConditionFromArray();
        void CreateOrConditionFromNativeArray();
        void CreateNotCondition();
        void AddAutomationEventHandler();
        void RemoveAutomationEventHandler();
        void AddPropertyChangedEventHandlerNativeArray();
        void AddPropertyChangedEventHandler();
        void RemovePropertyChangedEventHandler();
        void AddStructureChangedEventHandler();
        void RemoveStructureChangedEventHandler();
        void AddFocusChangedEventHandler();
        void RemoveFocusChangedEventHandler();
        void RemoveAllEventHandlers();
        void CreateProxyFactoryMapping();
        void PropertyIdToName();
        void NameToPropertyId();
        void ControlTypeToName();
        void NameToControlType();
        void GetPatternProgrammaticName();
        void GetProgrammaticNameConverter();
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("ff731470-0327-4b3d-9bd2-81c56c03b722")]
    internal interface IUIAutomationCondition
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("d22108aa-8ac5-46a4-a310-1e09e94d097f")]
    internal interface IUIAutomationElement
    {
        void SetFocus();
        void GetRuntimeId();
        void FindFirstBuildCache();
        void FindAllBuildCache();

        [PreserveSig]
        int FindFirst(int scope, IUIAutomationCondition condition, out IUIAutomationElement element);

        [PreserveSig]
        int FindAll(int scope, IUIAutomationCondition condition, out IUIAutomationElementArray elements);

        void BuildUpdatedCache();
        void GetCurrentPropertyValue();
        void GetCurrentPropertyValueEx();
        void GetCachedPropertyValue();
        void GetCachedPropertyValueEx();
        void GetCurrentPattern();
        void GetCachedPattern();
        void GetCurrentPatternAs();
        void GetCachedPatternAs();
        void GetCurrentPropertyValueWithName();
        void GetCachedPropertyValueWithName();

        [PreserveSig]
        int get_CurrentControlType(out int controlType);

        void get_CurrentAutomationId();

        [PreserveSig]
        int get_CurrentBoundingRectangle(out Interop.NativeMethods.RECT rect);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("00f40187-7f81-4ca9-9bb0-d485d738e3ec")]
    internal interface IUIAutomationElementArray
    {
        [PreserveSig]
        int get_Length(out int length);

        [PreserveSig]
        int GetElement(int index, out IUIAutomationElement element);
    }
}
