using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DailyMealPlannerExtended.Models;
using DailyMealPlannerExtended.Services;

namespace DailyMealPlannerExtended.ViewModels;

public enum ProductDetailMode
{
    Catalog,        // Viewing from catalog - show "Add to Meal Plan" button
    EditMealItem,   // Editing current day meal item - show editable weight controls
    ViewMealItem    // Viewing past day meal item - show read-only weight data
}

public partial class ProductDetailViewModel : ViewModelBase
{
    private readonly MealPlanViewModel? _mealPlanViewModel;
    private readonly CloudflareAIService _aiService;
    private readonly ImageService _imageService;

    [ObservableProperty]
    private Product? _product;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentWeight))]
    [NotifyPropertyChangedFor(nameof(CurrentServings))]
    [NotifyPropertyChangedFor(nameof(PhotoButtonText))]
    [NotifyPropertyChangedFor(nameof(ShowNoteReadOnly))]
    private MealPlanItem? _mealPlanItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAddToMealPlanButton))]
    [NotifyPropertyChangedFor(nameof(ShowWeightControls))]
    [NotifyPropertyChangedFor(nameof(IsWeightEditable))]
    [NotifyPropertyChangedFor(nameof(ShowImageAndNote))]
    [NotifyPropertyChangedFor(nameof(ShowNoteReadOnly))]
    [NotifyPropertyChangedFor(nameof(CurrentWeight))]
    [NotifyPropertyChangedFor(nameof(CurrentServings))]
    [NotifyPropertyChangedFor(nameof(CanGenerateImage))]
    private ProductDetailMode _mode = ProductDetailMode.Catalog;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGenerateImage))]
    private bool _isGeneratingImage;

    public bool CanGenerateImage => Mode == ProductDetailMode.EditMealItem && !IsGeneratingImage && _aiService.IsConfigured();

    partial void OnMealPlanItemChanged(MealPlanItem? oldValue, MealPlanItem? newValue)
    {
        // Unsubscribe from old item
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= MealPlanItem_PropertyChanged;
        }

        // Subscribe to new item
        if (newValue != null)
        {
            newValue.PropertyChanged += MealPlanItem_PropertyChanged;
        }
    }

    private void MealPlanItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MealPlanItem.Image))
        {
            OnPropertyChanged(nameof(PhotoButtonText));
        }
        else if (e.PropertyName == nameof(MealPlanItem.Note))
        {
            OnPropertyChanged(nameof(ShowNoteReadOnly));
        }
    }

    // Computed properties for view visibility
    public bool ShowAddToMealPlanButton => Mode == ProductDetailMode.Catalog && !(_mealPlanViewModel?.IsReadOnly ?? false);
    public bool ShowWeightControls => (Mode == ProductDetailMode.EditMealItem || Mode == ProductDetailMode.ViewMealItem) && !(_mealPlanViewModel?.IsReadOnly ?? false);
    public bool IsWeightEditable => Mode == ProductDetailMode.EditMealItem && !(_mealPlanViewModel?.IsReadOnly ?? false);
    public bool ShowImageAndNote => Mode == ProductDetailMode.EditMealItem || Mode == ProductDetailMode.ViewMealItem;
    public bool ShowNoteReadOnly => Mode == ProductDetailMode.ViewMealItem && !string.IsNullOrEmpty(MealPlanItem?.Note);

    public double CurrentWeight => MealPlanItem?.Weight ?? 0;

    public double CurrentServings => Product?.Serving > 0 && MealPlanItem != null
        ? MealPlanItem.Weight / Product.Serving
        : 0;

    public string PhotoButtonText => string.IsNullOrEmpty(MealPlanItem?.Image) ? "Add" : "Change";

    public ProductDetailViewModel()
    {
        _aiService = new CloudflareAIService();
        _imageService = new ImageService();
    }

    public ProductDetailViewModel(MealPlanViewModel mealPlanViewModel) : this()
    {
        _mealPlanViewModel = mealPlanViewModel;

        // Subscribe to IsReadOnly and CurrentMealPlan changes
        _mealPlanViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MealPlanViewModel.IsReadOnly))
            {
                OnPropertyChanged(nameof(ShowAddToMealPlanButton));
                OnPropertyChanged(nameof(ShowWeightControls));
                OnPropertyChanged(nameof(IsWeightEditable));
            }
            else if (e.PropertyName == nameof(MealPlanViewModel.CurrentMealPlan))
            {
                // When meal plan changes, refresh product bindings by reassigning
                // This ensures description, labels, and categories are re-rendered
                var currentProduct = Product;
                if (currentProduct != null)
                {
                    Product = null;
                    Product = currentProduct;
                }
            }
        };
    }

    [RelayCommand]
    private void IncrementServing()
    {
        if (Product?.Serving > 0 && MealPlanItem != null && IsWeightEditable)
        {
            var currentServings = Math.Floor(MealPlanItem.Weight / Product.Serving);
            MealPlanItem.Weight = (currentServings + 1) * Product.Serving;
        }
    }

    [RelayCommand]
    private void DecrementServing()
    {
        if (Product?.Serving > 0 && MealPlanItem != null && IsWeightEditable)
        {
            var currentServings = Math.Ceiling(MealPlanItem.Weight / Product.Serving);
            var newServings = Math.Max(0, currentServings - 1);
            MealPlanItem.Weight = newServings * Product.Serving;
        }
    }

    [RelayCommand]
    private async Task SelectPhotoAsync()
    {
        try
        {
            // Get the top-level window to access the storage provider
            var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (topLevel == null || MealPlanItem == null) return;

            // Show open file dialog for images
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Photo",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Image Files")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var filePath = files[0].Path.LocalPath;
                var imageBytes = await System.IO.File.ReadAllBytesAsync(filePath);

                // Resize image to max height of 180px for reasonable file size
                var resizedImageBytes = await ResizeImageAsync(imageBytes, maxHeight: 180);
                MealPlanItem.Image = Convert.ToBase64String(resizedImageBytes);

                Logger.Instance.Information("Photo added to meal plan item (resized to max height 180px)");
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to select photo");
        }
    }

    [RelayCommand]
    private void RemovePhoto()
    {
        if (MealPlanItem != null)
        {
            MealPlanItem.Image = null;
            Logger.Instance.Information("Photo removed from meal plan item");
        }
    }

    [RelayCommand]
    private async Task GenerateAIImageAsync()
    {
        if (MealPlanItem == null || Product == null)
        {
            Logger.Instance.Warning("Cannot generate AI image: no meal plan item or product");
            return;
        }

        if (!_aiService.IsConfigured())
        {
            Logger.Instance.Warning("Cloudflare AI service not configured");
            return;
        }

        try
        {
            IsGeneratingImage = true;

            Logger.Instance.Information("Generating AI image for: {Product}", Product.Name);

            // Generate the image
            var alternateNames = Product.AltNames != null && Product.AltNames.Count > 0
                ? string.Join(", ", Product.AltNames)
                : null;

            var bitmap = await _aiService.GenerateProductImageAsync(
                Product.Name,
                alternateNames,
                Product.Description
            );

            if (bitmap != null)
            {
                // Convert bitmap to base64
                await using var stream = new System.IO.MemoryStream();
                bitmap.Save(stream);
                var imageBytes = stream.ToArray();

                // Resize to max height of 180px for consistency with photo uploads
                var resizedImageBytes = await ResizeImageAsync(imageBytes, maxHeight: 180);
                MealPlanItem.Image = Convert.ToBase64String(resizedImageBytes);

                Logger.Instance.Information("AI image generated and added to meal plan item");
            }
            else
            {
                Logger.Instance.Warning("AI image generation returned null");
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error(ex, "Failed to generate AI image");
        }
        finally
        {
            IsGeneratingImage = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        Product = null;
        MealPlanItem = null;
        Mode = ProductDetailMode.Catalog;
    }

    public void ShowProduct(Product product)
    {
        Product = product;
        MealPlanItem = null;
        Mode = ProductDetailMode.Catalog;
        IsVisible = true;
    }

    public void ShowMealPlanItem(MealPlanItem item, bool isEditable)
    {
        MealPlanItem = item;
        Product = item.Product;
        Mode = isEditable ? ProductDetailMode.EditMealItem : ProductDetailMode.ViewMealItem;
        IsVisible = true;
    }

    partial void OnMealPlanItemChanged(MealPlanItem? value)
    {
        if (value != null)
        {
            value.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MealPlanItem.Weight))
                {
                    OnPropertyChanged(nameof(CurrentWeight));
                    OnPropertyChanged(nameof(CurrentServings));
                }
            };
        }
    }


    private async Task<byte[]> ResizeImageAsync(byte[] imageBytes, int maxHeight)
    {
        return await _imageService.ResizeImageAsync(imageBytes, maxHeight);
    }
}
