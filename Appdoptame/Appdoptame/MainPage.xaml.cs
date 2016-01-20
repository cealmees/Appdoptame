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
                // Set blob properties of TodoItem.
                todoItem.ContainerName = "todoitemimages";

                // Use a unigue GUID to avoid collisions.
                todoItem.ResourceName = Guid.NewGuid().ToString();
            }

            // Send the item to be inserted. When blob properties are set this
            // generates an SAS in the response.
            await todoTable.InsertAsync(todoItem);

            // If we have a returned SAS, then upload the blob.
            if (!string.IsNullOrEmpty(todoItem.SasQueryString))
            {
                // Get the URI generated that contains the SAS 
                // and extract the storage credentials.
                StorageCredentials cred = new StorageCredentials(todoItem.SasQueryString);
                var imageUri = new Uri(todoItem.ImageUri);

                // Instantiate a Blob store container based on the info in the returned item.
                CloudBlobContainer container = new CloudBlobContainer(
                    new Uri(string.Format("https://{0}/{1}",
                        imageUri.Host, todoItem.ContainerName)), cred);

                // Get the new image as a stream.
                using (var inputStream = await media.OpenReadAsync())
                {
                    // Upload the new image as a BLOB from the stream.
                    CloudBlockBlob blobFromSASCredential =
                        container.GetBlockBlobReference(todoItem.ResourceName);
                    await blobFromSASCredential.UploadFromStreamAsync(inputStream);
                }

                // When you request an SAS at the container-level instead of the blob-level,
                // you are able to upload multiple streams using the same container credentials.

                await ResetCaptureAsync();
            }

            // Add the new item to the collection.
            items.Add(todoItem);
        }

        private async Task RefreshTodoItems()
        {
            MobileServiceInvalidOperationException exception = null;
            try
            {
                // This code refreshes the entries in the list view by querying the TodoItems table.
                // The query excludes completed TodoItems
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
            // This code takes a freshly completed TodoItem and updates the database. When the MobileService 
            // responds, the item is removed from the list 
            await todoTable.UpdateAsync(item);
            items.Remove(item);
            ListItems.Focus(Windows.UI.Xaml.FocusState.Unfocused);

            //await SyncAsync(); // offline sync
        }

        private async void ButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            ButtonRefresh.IsEnabled = false;

            //await SyncAsync(); // offline sync
            await RefreshTodoItems();

            ButtonRefresh.IsEnabled = true;
        }

        private async void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            

            Geolocator geo = new Geolocator();
            // await this because we don't know hpw long it will take to complete and we don't want to block the UI
            Geoposition pos = await geo.GetGeopositionAsync(); // get the raw geoposition data
            double lat = pos.Coordinate.Point.Position.Latitude; // current latitude
            double longt = pos.Coordinate.Point.Position.Longitude; // current longitude

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
            //await InitLocalStoreAsync(); // offline sync
            await RefreshTodoItems();
        }

        #region Offline sync

        //private async Task InitLocalStoreAsync()
        //{
        //    if (!App.MobileService.SyncContext.IsInitialized)
        //    {
        //        var store = new MobileServiceSQLiteStore("localstore.db");
        //        store.DefineTable<TodoItem>();
        //        await App.MobileService.SyncContext.InitializeAsync(store);
        //    }
        //
        //    await SyncAsync();
        //}

        //private async Task SyncAsync()
        //{
        //    await App.MobileService.SyncContext.PushAsync();
        //    await todoTable.PullAsync("todoItems", todoTable.CreateQuery());
        //}

        #endregion 
            
        StorageFile media = null;
        MediaCapture cameraCapture;
        bool IsCaptureInProgress;

        private async Task CaptureImage()
        {

            cameraCapture = new MediaCapture();
            cameraCapture.Failed += cameraCapture_Failed;

            // Initialize the camera for capture.
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
            // Block multiple taps.
            if (!IsCaptureInProgress)
            {
                IsCaptureInProgress = true;

                // Create the temporary storage file.
                media = await ApplicationData.Current.LocalFolder
                    .CreateFileAsync("capture_file.jpg", CreationCollisionOption.ReplaceExisting);

                // Take the picture and store it locally as a JPEG.
                await cameraCapture.CapturePhotoToStorageFileAsync(
                    ImageEncodingProperties.CreateJpeg(), media);

                captureButtons.Visibility = Visibility.Visible;

                // Use the stored image as the preview source.
                BitmapImage tempBitmap = new BitmapImage(new Uri(media.Path));
                imagePreview.Source = tempBitmap;
                imagePreview.Visibility = Visibility.Visible;
                previewElement.Visibility = Visibility.Collapsed;
                IsCaptureInProgress = false;
            }
        }

        private async void cameraCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            // It's safest to return this back onto the UI thread to show the message dialog.
            MessageDialog dialog = new MessageDialog(errorEventArgs.Message);
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                async () => { await dialog.ShowAsync(); });
        }

        private async void ButtonCapture_Click(object sender, RoutedEventArgs e)
        {
            // Prevent multiple initializations.
            ButtonCapture.IsEnabled = false;

            await CaptureImage();
            show.Visibility = Visibility.Collapsed;
            fotografia.Visibility = Visibility.Visible;
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

        }

        private async void ButtonRetake_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Reset the capture and then start again.
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

            // Make sure we stop the preview and release resources.
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
