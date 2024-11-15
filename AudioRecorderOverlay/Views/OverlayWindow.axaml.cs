using Avalonia.Controls;
using System;
using AudioRecorderOverlay.Services;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using FluentAvalonia.Styling;
using FluentAvalonia.UI.Windowing;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using FluentAvalonia.UI.Media;
using System.Linq;
using AudioRecorderOverlay.ViewModels;

namespace AudioRecorderOverlay.Views;

public sealed partial class OverlayWindow : AppWindow
{
    private IInputElement? _previousFocusedElement;
    private bool _isMenuFocused;

    public OverlayWindow()
    {
        InitializeComponent();

        Deactivated += (_, _) => { if (IsVisible) Hide(); };
        AddHandler(KeyDownEvent, OverlayWindowKeyHandler);
        AddHandler(KeyDownEvent, OverlayWindowTabKeyHandler, RoutingStrategies.Tunnel);

        if (Application.Current != null)
            Application.Current.ActualThemeVariantChanged += OnActualThemeVariantChanged;

        HotkeyManager.RegisterOverlayHotkey(ToggleOverlay);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (IsWindows11 && ActualThemeVariant != FluentAvaloniaTheme.HighContrastTheme)
            TryEnableMicaEffect();
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (IsWindows11)
            if (ActualThemeVariant != FluentAvaloniaTheme.HighContrastTheme)
            {
                TryEnableMicaEffect();
            }
            else
            {
                ClearValue(BackgroundProperty);
                ClearValue(TransparencyBackgroundFallbackProperty);
            }
    }

    private void TryEnableMicaEffect()
    {
        return;
        // TransparencyBackgroundFallback = Brushes.Transparent;
        // TransparencyLevelHint = WindowTransparencyLevel.Mica;

        // The background colors for the Mica brush are still based around SolidBackgroundFillColorBase resource
        // BUT since we can't control the actual Mica brush color, we have to use the window background to create
        // the same effect. However, we can't use SolidBackgroundFillColorBase directly since its opaque, and if
        // we set the opacity the color become lighter than we want. So we take the normal color, darken it and 
        // apply the opacity until we get the roughly the correct color
        // NOTE that the effect still doesn't look right, but it suffices. Ideally we need access to the Mica
        // CompositionBrush to properly change the color but I don't know if we can do that or not
        if (ActualThemeVariant == ThemeVariant.Dark)
        {
            var color = this.TryFindResource("SolidBackgroundFillColorBase",
                ThemeVariant.Dark, out var value) ? (Color2)(Color)value : new Color2(32, 32, 32);

            color = color.LightenPercent(-0.8f);

            Background = new ImmutableSolidColorBrush(color, 0.9);
        }
        else if (ActualThemeVariant == ThemeVariant.Light)
        {
            // Similar effect here
            var color = this.TryFindResource("SolidBackgroundFillColorBase",
                ThemeVariant.Light, out var value) ? (Color2)(Color)value : new Color2(243, 243, 243);

            color = color.LightenPercent(0.5f);

            Background = new ImmutableSolidColorBrush(color, 0.9);
        }
    }

    private void HideOverlay(object? sender, RoutedEventArgs e)
    {
        if (IsVisible)
            Hide();
    }

    private void ToggleOverlay()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        HotkeyManager.UnregisterHotkey();
        base.OnClosed(e);
    }

    private void MainMenu_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Menu menu || menu.SelectedItem is not MenuItem selectedItem) return;

        var column = Grid.GetColumn(selectedItem);
        var row = Grid.GetRow(selectedItem);
        var columnSpan = Grid.GetColumnSpan(selectedItem);
        var rowSpan = Grid.GetRowSpan(selectedItem);

        switch (e.Key)
        {
            case Key.Left:
                MoveFocusToMenuItem(menu, column - columnSpan, row);
                break;
            case Key.Right:
                MoveFocusToMenuItem(menu, column + columnSpan, row);
                break;
            case Key.Up:
                MoveFocusToMenuItem(menu, column, row - rowSpan);
                break;
            case Key.Down:
                MoveFocusToMenuItem(menu, column, row + rowSpan);
                break;
        }
    }

    private void MoveFocusToMenuItem(Menu menu, int targetColumn, int targetRow)
    {
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            var itemColumn = Grid.GetColumn(item);
            var itemRow = Grid.GetRow(item);
            var columnSpan = Grid.GetColumnSpan(item);
            var rowSpan = Grid.GetRowSpan(item);

            var isInColumnRange = targetColumn >= itemColumn && targetColumn < itemColumn + columnSpan;
            var isInRowRange = targetRow >= itemRow && targetRow < itemRow + rowSpan;

            if (isInColumnRange && isInRowRange)
            {
                item.IsSubMenuOpen = true;
                item.Focus();
                break;
            }
        }
    }

    private void OverlayWindowKeyHandler(object? sender, KeyEventArgs e)
    {
        if (DataContext is OverlayWindowViewModel viewModel && !viewModel.IsContentDialogOpened)
        {
            if (e.Key == Key.Escape || e.Key == Key.LWin || e.Key == Key.RWin)
            {
                HideOverlay(sender, e);
                e.Handled = true;
            }

            if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
            {
                if (!_isMenuFocused)
                {
                    _previousFocusedElement = FocusManager?.GetFocusedElement();

                    if (MainMenu.Items.OfType<MenuItem>().FirstOrDefault() is MenuItem firstItem)
                    {
                        firstItem.IsSubMenuOpen = true;
                        firstItem.Focus();
                        _isMenuFocused = true;
                    }
                }
                else
                {
                    _previousFocusedElement?.Focus();
                    _previousFocusedElement = null;
                    _isMenuFocused = false;
                }

                e.Handled = true;
            }
        }
    }

    private void OverlayWindowTabKeyHandler(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && _isMenuFocused)
        {
            var selectedItem = FocusManager?.GetFocusedElement() as MenuItem;
            if (selectedItem != null)
            {
                selectedItem.IsSubMenuOpen = false;
                selectedItem.IsSelected = false;
            }

            _previousFocusedElement = selectedItem;
            _isMenuFocused = false;
        }
    }
}