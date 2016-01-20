using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Xaml.Input;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Devices.Geolocation;
using Windows.UI.Core;

namespace Appdoptame
{
    sealed partial class MainPage : Page
    {

        private MobileServiceCollection<DataModel.TodoItem, DataModel.TodoItem> items;
        private IMobileServiceTable<DataModel.TodoItem> todoTable = App.MobileService.GetTable<DataModel.TodoItem>();

        public MainPage()
     
        {
            this.InitializeComponent();
        }


        private async Task InsertTodoItem(DataModel.TodoItem todoItem)
        {
            string errorString = string.Empty;

            if (media != null)
            {
                todoItem.ContainerName = "todoitemimages";
                todoItem.ResourceName = Guid.NewGuid().ToString();
            }

            await todoTable.InsertAsync(todoItem);

            if (!string.IsNullOrEmpty(todoItem.SasQueryString))
            {
                StorageCredentials cred = new StorageCredentials(todoItem.SasQueryString);
                var imageUri = new Uri(todoItem.ImageUri);

                CloudBlobContainer container = new CloudBlobContainer(
                    new Uri(string.Format("https://{0}/{1}",
                        imageUri.Host, todoItem.ContainerName)), cred);

                using (var inputStream = await media.OpenReadAsync())
                {
                    CloudBlockBlob blobFromSASCredential =
                        container.GetBlockBlobReference(todoItem.ResourceName);
                    await blobFromSASCredential.UploadFromStreamAsync(inputStream);
                }
                
                await ResetCaptureAsync();
            }

            items.Add(todoItem);
        }

        private async Task RefreshTodoItems()
        {
            MobileServiceInvalidOperationException exception = null;
            try
            {
                items = await todoTable
                    .Where(todoItem => todoItem.Complete == false)
                    .ToCollectionAsync();
            }
            catch (MobileServiceInvalidOperationException e)
            {
                exception = e;
            }

            if (exception != null)
            {
                await new MessageDialog(exception.Message, "Error loading items").ShowAsync();
            }
            else
            {
                ListItems.ItemsSource = items;
                this.ButtonSave.IsEnabled = true;
            }
        }

        private async Task UpdateCheckedTodoItem(DataModel.TodoItem item)
        {
            await todoTable.UpdateAsync(item);
            items.Remove(item);
            ListItems.Focus(Windows.UI.Xaml.FocusState.Unfocused);

        }

        private async void ButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            ButtonRefresh.IsEnabled = false;

            await RefreshTodoItems();

            ButtonRefresh.IsEnabled = true;
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            

            Geolocator geo = new Geolocator();
            Geoposition pos = await geo.GetGeopositionAsync(); 
            double lat = pos.Coordinate.Point.Position.Latitude;
            double longt = pos.Coordinate.Point.Position.Longitude; 

            var todoItem = new DataModel.TodoItem
            {

                Text = TextInput.Text,
                Descripcion = descripcion.Text,
                Latitud = lat.ToString(),
                Longitud = longt.ToString()

            };
            await InsertTodoItem(todoItem);
            subida.Visibility = Visibility.Collapsed;
            show.Visibility = Visibility.Visible;

        }

        private async void CheckBoxComplete_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            DataModel.TodoItem item = cb.DataContext as DataModel.TodoItem;
            await UpdateCheckedTodoItem(item);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await RefreshTodoItems();
        }

            
        StorageFile media = null;
        MediaCapture cameraCapture;
        bool IsCaptureInProgress;

        private async Task CaptureImage()
        {

            cameraCapture = new MediaCapture();
            cameraCapture.Failed += cameraCapture_Failed;

            await cameraCapture.InitializeAsync();

#if WINDOWS_PHONE_APP
    cameraCapture.SetPreviewRotation(VideoRotation.Clockwise90Degrees);
    cameraCapture.SetRecordRotation(VideoRotation.Clockwise90Degrees);
#endif

            captureGrid.Visibility = Windows.UI.Xaml.Visibility.Visible;
            previewElement.Visibility = Windows.UI.Xaml.Visibility.Visible;
            previewElement.Source = cameraCapture;
            await cameraCapture.StartPreviewAsync();
        }

        private async void previewElement_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (!IsCaptureInProgress)
            {
                IsCaptureInProgress = true;

                media = await ApplicationData.Current.LocalFolder
                    .CreateFileAsync("capture_file.jpg", CreationCollisionOption.ReplaceExisting);

                await cameraCapture.CapturePhotoToStorageFileAsync(
                    ImageEncodingProperties.CreateJpeg(), media);

                captureButtons.Visibility = Visibility.Visible;

                BitmapImage tempBitmap = new BitmapImage(new Uri(media.Path));
                imagePreview.Source = tempBitmap;
                imagePreview.Visibility = Visibility.Visible;
                previewElement.Visibility = Visibility.Collapsed;
                IsCaptureInProgress = false;
            }
        }

        private async void cameraCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            MessageDialog dialog = new MessageDialog(errorEventArgs.Message);
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                async () => { await dialog.ShowAsync(); });
        }

        private async void ButtonCapture_Click(object sender, RoutedEventArgs e)
        {
            ButtonCapture.IsEnabled = false;

            await CaptureImage();
            show.Visibility = Visibility.Collapsed;
            fotografia.Visibility = Visibility.Visible;
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

        }

        private async void ButtonRetake_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await ResetCaptureAsync();
            await CaptureImage();
        }

        private async void ButtonCancel_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await ResetCaptureAsync();
        }

        private async Task ResetCaptureAsync()
        {
            captureGrid.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            imagePreview.Visibility = Visibility.Collapsed;
            captureButtons.Visibility = Visibility.Collapsed;
            previewElement.Source = null;
            ButtonCapture.IsEnabled = true;

            await cameraCapture.StopPreviewAsync();
            cameraCapture.Dispose();
            media = null;
        }

        public void GetLocation()
        {

        }

        private void next_Click(object sender, RoutedEventArgs e)
        {
            fotografia.Visibility = Visibility.Collapsed;
            subida.Visibility = Visibility.Visible;
        }

        private void ScrollToBottom()
        {
            var selectedIndex = ListItems.Items.Count - 1;
            if (selectedIndex < 0)
                return;

            ListItems.SelectedIndex = selectedIndex;
            ListItems.UpdateLayout();

            ListItems.ScrollIntoView(ListItems.SelectedItem);
        }
    }
}
