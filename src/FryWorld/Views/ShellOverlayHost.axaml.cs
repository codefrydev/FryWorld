using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using FryWorld.ViewModels;

namespace FryWorld.Views;

public partial class ShellOverlayHost : UserControl
{
    private MainViewModel? _observedVm;

    public ShellOverlayHost()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_observedVm != null)
        {
            _observedVm.ActiveOverlays.CollectionChanged -= OnActiveOverlaysChanged;
        }

        _observedVm = DataContext as MainViewModel;

        if (_observedVm != null)
        {
            _observedVm.ActiveOverlays.CollectionChanged += OnActiveOverlaysChanged;
            ClampAndPositionOverlays();
        }
    }

    private void OnActiveOverlaysChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ClampAndPositionOverlays();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ClampAndPositionOverlays();
    }

    private Point _dragStartPoint;
    private bool _isDraggingStandardHeader;

    private bool _isResizing;
    private Point _resizeStartPointer;
    private double _resizeStartWidth;
    private double _resizeStartHeight;
    private ActiveOverlayViewModel? _resizingVm;

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control ctrl && ctrl.DataContext is ActiveOverlayViewModel vm && _observedVm != null)
        {
            vm.BringToFront(_observedVm.ActiveOverlays);
        }
    }

    private void OnStandardHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDraggingStandardHeader = true;
            var root = this.VisualRoot as Visual ?? this;
            _dragStartPoint = e.GetPosition(root);
            e.Pointer.Capture(sender as Control);

            if (sender is Control ctrl && ctrl.DataContext is ActiveOverlayViewModel vm && _observedVm != null)
            {
                vm.BringToFront(_observedVm.ActiveOverlays);
            }
            e.Handled = true;
        }
    }

    private void OnStandardHeaderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingStandardHeader && sender is Control ctrl && ctrl.DataContext is ActiveOverlayViewModel vm)
        {
            var root = this.VisualRoot as Visual ?? this;
            var currentPoint = e.GetPosition(root);
            var deltaX = currentPoint.X - _dragStartPoint.X;
            var deltaY = currentPoint.Y - _dragStartPoint.Y;
            _dragStartPoint = currentPoint;

            var maxCanvasX = Bounds.Width > 100 ? Math.Max(20, Bounds.Width - 80) : double.PositiveInfinity;
            var maxCanvasY = Bounds.Height > 100 ? Math.Max(20, Bounds.Height - 60) : double.PositiveInfinity;

            vm.X = Math.Clamp(vm.X + deltaX, 0, maxCanvasX);
            vm.Y = Math.Clamp(vm.Y + deltaY, 0, maxCanvasY);
            e.Handled = true;
        }
    }

    private void OnStandardHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDraggingStandardHeader)
        {
            _isDraggingStandardHeader = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnResizeBottomRightPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && sender is Control ctrl && ctrl.DataContext is ActiveOverlayViewModel vm)
        {
            _isResizing = true;
            _resizingVm = vm;
            _resizeStartWidth = vm.Width;
            _resizeStartHeight = vm.Height;

            var root = this.VisualRoot as Visual ?? this;
            _resizeStartPointer = e.GetPosition(root);

            e.Pointer.Capture(ctrl);

            if (_observedVm != null)
            {
                vm.BringToFront(_observedVm.ActiveOverlays);
            }
            e.Handled = true;
        }
    }

    private void OnResizePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isResizing && _resizingVm != null)
        {
            var root = this.VisualRoot as Visual ?? this;
            var currentPoint = e.GetPosition(root);
            var deltaX = currentPoint.X - _resizeStartPointer.X;
            var deltaY = currentPoint.Y - _resizeStartPointer.Y;

            var minW = Math.Max(160, _resizingVm.Descriptor.MinWidth);
            var minH = Math.Max(120, _resizingVm.Descriptor.MinHeight);
            var maxW = Bounds.Width > 200 ? Bounds.Width - _resizingVm.X - 10 : double.PositiveInfinity;
            var maxH = Bounds.Height > 200 ? Bounds.Height - _resizingVm.Y - 10 : double.PositiveInfinity;

            _resizingVm.Width = Math.Clamp(_resizeStartWidth + deltaX, minW, Math.Max(minW, maxW));
            _resizingVm.Height = Math.Clamp(_resizeStartHeight + deltaY, minH, Math.Max(minH, maxH));
            e.Handled = true;
        }
    }

    private void OnResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isResizing)
        {
            _isResizing = false;
            _resizingVm = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void ClampAndPositionOverlays()
    {
        if (_observedVm == null || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        foreach (var overlay in _observedVm.ActiveOverlays)
        {
            var maxCanvasX = Math.Max(20, Bounds.Width - 100);
            var maxCanvasY = Math.Max(20, Bounds.Height - 60);

            if (overlay.X > maxCanvasX) overlay.X = Math.Max(20, maxCanvasX);
            if (overlay.Y > maxCanvasY) overlay.Y = Math.Max(20, maxCanvasY);
        }
    }
}
