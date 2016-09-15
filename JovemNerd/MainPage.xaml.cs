using JovemNerd.BackgroundTasksHandlers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.ApplicationModel.Background;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace JovemNerd
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        ObservableCollection<Episode> episodes_list = new ObservableCollection<Episode>();

        Boolean podcastLoaded = false;
        private Episode episode;
        DownloadOperation download;
        CancellationTokenSource cancellationToken;
        Windows.Networking.BackgroundTransfer.BackgroundDownloader downloader = new Windows.Networking.BackgroundTransfer.BackgroundDownloader();
        DispatcherTimer timer = new DispatcherTimer();
        SystemMediaTransportControls systemControls;
        string file_playing;
        string file_downloading;
        string current_insertion;
        
        /// <summary>
        /// Inicialização da tela principal
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            BackgroundTasksFactory.RegisterBackgroundTask("JNBackgroundTask.BackgroundPlayer", "BackgroundPlayer", new SystemTrigger(SystemTriggerType.InternetAvailable, false), null);

            hideStatusBar();

            timer.Interval = TimeSpan.FromMilliseconds(1000);
            timer.Tick += progressPlayer;
            
            // Permite controlar o som através de botões do sistema (enquanto em background)
            systemControls = SystemMediaTransportControls.GetForCurrentView();
            systemControls.ButtonPressed += SystemControls_ButtonPressed;
            systemControls.IsPlayEnabled = true;
            systemControls.IsPauseEnabled = true;
            systemControls.PlaybackStatus = MediaPlaybackStatus.Stopped;

            PlayerMedia.Width = 100;
            PlayerMedia.Height = 100;
            PlayerMedia.AreTransportControlsEnabled = true;
            PlayerMedia.CurrentStateChanged += MediaElement_CurrentStateChanged;
            PlayerMedia.AudioCategory = AudioCategory.BackgroundCapableMedia;

            sliderSeek.Value = PlayerMedia.Position.Seconds;

            if (podcastLoaded == false)
            {
                loadNerdcast();
            }
        }


        /// <summary>
        /// Oculta a barra de status em dispositivos mobile
        /// </summary>
        private async void hideStatusBar()
        {
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().HideAsync();
            }
        }
        
        /// <summary>
        /// Carga da lista de podcasts
        /// </summary>
        private async void loadNerdcast()
        {
            StorageFolder dataFolder = await KnownFolders.MusicLibrary.CreateFolderAsync("JovemNerd", CreationCollisionOption.OpenIfExists);
            StorageFile nerdcastFile = null;
            try
            {
                nerdcastFile = await dataFolder.GetFileAsync("nerdcast.json");
            }
            catch (Exception ex) // Aplicativo aberto pela primeira vez (não vai ter ainda o json com os podcasts)
            {
                PivotMainPage.SelectedItem = UpdatePivot; // Chama a tela de atualização dos podcasts
                return;
            }
            var jsonString = await Windows.Storage.FileIO.ReadTextAsync(nerdcastFile);

            episodes_list.Clear(); // Limpa a lista de podcasts (para caso de atualização do feed)

            JsonArray root = JsonValue.Parse(jsonString).GetArray();
            for (uint i = 0; i < root.Count; i++) // Monta a lista de podcasts para serem exibidos na tela inicial
            {
                var episode_item = new Episode
                {
                    id = root.GetObjectAt(i).GetNamedNumber("id").ToString(),
                    title = root.GetObjectAt(i).GetNamedString("episode") + " - " + root.GetObjectAt(i).GetNamedString("title"),
                    description = root.GetObjectAt(i).GetNamedString("description"),
                    image = root.GetObjectAt(i).GetNamedString("image"),
                    duration = root.GetObjectAt(i).GetNamedNumber("duration"),
                    episode = root.GetObjectAt(i).GetNamedString("episode"),
                    audio_high = root.GetObjectAt(i).GetNamedString("audio_high"),
                    audio_medium = root.GetObjectAt(i).GetNamedString("audio_medium"),
                    audio_low = root.GetObjectAt(i).GetNamedString("audio_low"),
                    pub_date = root.GetObjectAt(i).GetNamedString("pub_date"),
                    product = root.GetObjectAt(i).GetNamedString("product_name"),
                    subject = root.GetObjectAt(i).GetNamedString("subject"),
                    slug = root.GetObjectAt(i).GetNamedString("slug")
                };
                var insertions = root.GetObjectAt(i).GetNamedArray("insertions");

                episode_item.insertions = new List<Insertions>();
                for (uint x = 0; x < insertions.Count; x++)
                {
                    var insertion_data = JsonValue.Parse(insertions.GetObjectAt(x).ToString()).GetObject();
                    var insertion = new Insertions
                    {
                        id = insertion_data["id"].ToString(),
                        image = insertion_data["image"].ToString(),
                        start_time = insertion_data["start-time"].ToString(),
                        end_time = insertion_data["end-time"].ToString()
                    };
                    episode_item.insertions.Add(insertion);
                }
                episodes_list.Add(episode_item);
            };
            listNerdcast.ItemsSource = episodes_list;
        }

        /// <summary>
        /// Abre um episódio no player
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void listNerdcast_ItemClick(object sender, ItemClickEventArgs e)
        {
            episode = (Episode)e.ClickedItem; // Carrega o objeto do episódio

            episode_title.Text = episode.title; // Título

            BitmapImage image = new BitmapImage(new Uri(episode.image)); // Imagem do episódio
            imgEpisode.Source = image;

            // Abre o bloco do player
            PivotMainPage.SelectedItem = PlayerPivot;

            // Habilita o botão de download
            btnDownload.IsEnabled = true;

            // Nome do arquivo
            var filename = episode.slug + ".mp3";
            // Verifica se está fazendo download de outro episódio
            if (filename != file_downloading)
            {
                btnDownload.IsEnabled = true; // Desabilita o botão de download
                btnDownload.Content = "Download"; // Ajusta texto do botão de download
            }

            try
            {
                StorageFolder destinationFolder = await KnownFolders.MusicLibrary.CreateFolderAsync("JovemNerd", CreationCollisionOption.OpenIfExists);
                StorageFile destinationFile = await destinationFolder.GetFileAsync(filename);
                btnDownload.IsEnabled = false;
                btnPlay.IsEnabled = true;
                btPause.IsEnabled = true;
            }
            catch (Exception ex)
            {
                btnPlay.IsEnabled = false;
                btPause.IsEnabled = false;
            }
        }

        /// <summary>
        /// Controle de atualização da barra de progresso
        /// </summary>
        /// <param name="e"></param>
        /// <param name="sender"></param>
        void progressPlayer(object sender, object e)
        {
            var seconds = PlayerMedia.Position.TotalSeconds;
            sliderSeek.Value = seconds; // Atualiza a barra de progresso do player
            createInsertions(episode.insertions, seconds); // Criação de imagens incluídas durante o podcast
        }

        /// <summary>
        /// Adiciona imagens citadas durante o podcast
        /// </summary>
        /// <param name="insertions"></param>
        /// <param name="seconds"></param>
        void createInsertions(List<Insertions> insertions, double seconds)
        {
            bool found_insertion = false;
            for (int i = 0; i < insertions.Count; i++)
            {
                var segundos = (int)seconds;
                if (Convert.ToInt32(insertions[i].start_time) <= (int)seconds && Convert.ToInt32(insertions[i].end_time) >= (int)seconds)
                {
                    var insertion = insertions[i].image.Replace("\"", ""); // Ajusta a URL das imagens removendo aspas duplas que circulam o endereço
                    if (current_insertion != insertion) // Evita recriar a imagem e alterar o source do objeto
                    {
                        BitmapImage image = new BitmapImage(new Uri(insertion));
                        imgInsertions.Source = image; // Atualiza o endereço da imagem a ser exibida
                        found_insertion = true; // Controle para evitar seguir no loop
                        break;
                    }
                    current_insertion = insertions[i].image.Replace("\"", "");
                }
            }

            // Limpa o endereço da imagem caso não tenha nenhuma imagem disponível para o momento de reprodução
            if (found_insertion == false)
            {
                imgInsertions.Source = null;
            }
        }

        /// <summary>
        /// Carga da lista de podcasts
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            var filename = episode.slug + ".mp3";

            try
            {
                // Verifica se o arquivo já estava sendo executado
                if (filename != file_playing)
                {
                    // Interrompe qualquer reprodução e controle de progresso anterior
                    PlayerMedia.Position = new TimeSpan(0, 0, 0, 0);
                    PlayerMedia.Stop();
                    timer.Stop();

                    // Define o caminho para reproduzir o arquivo
                    StorageFolder destinationFolder = await KnownFolders.MusicLibrary.CreateFolderAsync("JovemNerd", CreationCollisionOption.OpenIfExists);
                    StorageFile destinationFile = await destinationFolder.GetFileAsync(filename);

                    PlayerMedia.AudioCategory = Windows.UI.Xaml.Media.AudioCategory.Media;
                    PlayerMedia.SetSource(await destinationFile.OpenAsync(FileAccessMode.Read), destinationFile.FileType);
                    file_playing = filename;
                    
                    // Get the updater.
                    var updater = systemControls.DisplayUpdater;
                    updater.Type = MediaPlaybackType.Music;
                    updater.MusicProperties.Title = episode.title;
                    updater.Update();

                }
                else
                {
                    PlayerMedia.Position = new TimeSpan(0, 0, 0, (int)sliderSeek.Value);
                }

                // Atualiza a barra de progresso de reprodução 
                sliderSeek.Maximum = Convert.ToDouble(episode.duration);
                PlayerMedia.Play();
                timer.Start();
            }
            catch (FileNotFoundException ex)
            {
                var dialog = new MessageDialog("Faça o download do arquivo primeiro.");
                await dialog.ShowAsync();
            }
        }

        /// <summary>
        /// Download de episódio do podcast
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            var filename = episode.slug + ".mp3";
            file_downloading = filename;
            try
            {
                Uri source = new Uri(episode.audio_high);

                // Define o local de armazenagem do arquivo
                StorageFolder destinationFolder = await KnownFolders.MusicLibrary.CreateFolderAsync("JovemNerd", CreationCollisionOption.OpenIfExists);
                StorageFile destinationFile = await destinationFolder.CreateFileAsync(filename, CreationCollisionOption.GenerateUniqueName);

                downloader = new BackgroundDownloader();
                download = downloader.CreateDownload(source, destinationFile);

                Progress<DownloadOperation> progress = new Progress<DownloadOperation>(progressChanged);
                cancellationToken = new CancellationTokenSource();
                PivotMainPage.SelectedItem = DownloadPivot;
                await download.StartAsync().AsTask(cancellationToken.Token, progress);
            }
            catch (Exception error)
            {
                var dialog = new MessageDialog("Ocorreu um erro ao tentar baixar o arquivo. Erro: " + error.Message);
                await dialog.ShowAsync();
            }
        }

        /// <summary>
        /// Controle do progresso do download do episódio
        /// </summary>
        /// <param name="download"></param>
        private async void progressChanged(DownloadOperation download)
        {
            int progress = (int)(100 * ((double)download.Progress.BytesReceived / (double)download.Progress.TotalBytesToReceive));
            fileDownloadingName.Text = episode.title;
            progressDownload.Text = progress + "%";
            switch (download.Progress.Status)
            {
                case BackgroundTransferStatus.Running:
                    {
                        break;
                    }
                case BackgroundTransferStatus.PausedByApplication:
                    {
                        break;
                    }
                case BackgroundTransferStatus.PausedCostedNetwork:
                    {
                        break;
                    }
                case BackgroundTransferStatus.PausedNoNetwork:
                    {
                        break;
                    }
                case BackgroundTransferStatus.Error:
                    {
                        btnDownload.IsEnabled = true;
                        var dialog = new MessageDialog("Ocorreu um erro ao tentar baixar o arquivo.");
                        await dialog.ShowAsync();
                        break;
                    }
            }
        }
        
        /// <summary>
        /// Ação de pause do player
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btPause_Click(object sender, RoutedEventArgs e)
        {
            PlayerMedia.Pause();
            timer.Stop();
        }

        /// <summary>
        /// Mudança de status do elemento de reprodução de mídia
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MediaElement_CurrentStateChanged(object sender, RoutedEventArgs e)
        {
            switch (PlayerMedia.CurrentState)
            {
                case MediaElementState.Playing:
                    systemControls.PlaybackStatus = MediaPlaybackStatus.Playing;
                    break;
                case MediaElementState.Paused:
                    systemControls.PlaybackStatus = MediaPlaybackStatus.Paused;
                    break;
                case MediaElementState.Stopped:
                    systemControls.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    break;
                case MediaElementState.Closed:
                    systemControls.PlaybackStatus = MediaPlaybackStatus.Closed;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Controle de mídia quando o aplicativo está em background (exemplo: celular bloqueado)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void SystemControls_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    PlayMedia();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    PauseMedia();
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    StopMedia();
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Ação de parar do controle de mídia em background
        /// </summary>
        private async void StopMedia()
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                PlayerMedia.Stop();
            });
        }

        /// <summary>
        /// Ação de reproduzir do controle de mídia em background
        /// </summary>
        async void PlayMedia()
        {
            Debug.WriteLine(PlayerMedia);
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (PlayerMedia.CurrentState == MediaElementState.Playing)
                    PlayerMedia.Pause();
                else
                    PlayerMedia.Play();
            });
        }

        /// <summary>
        /// Ação de pausar do controle de mídia em background
        /// </summary>
        async void PauseMedia()
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                PlayerMedia.Pause();
            });
        }

        /// <summary>
        /// Clique no controle de tempo de reprodução (ainda não está funcionando)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sliderSeek_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var slider = sender as Slider;
            PlayerMedia.Position = new TimeSpan(0, 0, 0, (int)slider.Value);

        }

        /// <summary>
        /// Ação de atualização do nerdcast
        /// </summary>
        /// <seealso cref="updateNerdcast()"/>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnUpdateNerdcast_Click(object sender, RoutedEventArgs e)
        {
            updateNerdcast();
        }

        /// <summary>
        /// Atualiza o feed do nerdcast buscando via api do site
        /// O aplicativo faz o download do arquivo (e armazena em [Musicas]data/nerdcast.json) e chama o método de atualização da lista de episódios
        /// </summary>
        /// <seealso cref="loadNerdcast()"/>
        private async void updateNerdcast()
        {
            progressUpdate.Text = "Carregando podcast...";
            var client = new HttpClient();
            //string jsonString = await response.Content.ReadAsStringAsync();
            string jsonString = "[";
            for (uint i = 1; i < 59; i++)
            {
                // Monta a URL para percorrer a paginação da api
                var url = "https://api.jovemnerd.com.br/wp-json/jovemnerd/v1/nerdcasts?search=&page=" + i.ToString();
                // Faz a requisição
                HttpResponseMessage response = await client.GetAsync(new Uri(url));
                if (jsonString != "[")
                {
                    jsonString += ","; // Junta a lista de episódios da página com uma vírgula
                }
                var fileContent = await response.Content.ReadAsStringAsync();
                // Remove o caracter "[" e "]" do início e fim do arquivo com o fim de unir os arrays do json
                var page = fileContent.Substring(1, fileContent.Length - 1);
                jsonString += page.Substring(0, page.Length - 1).Replace("null", "\"\""); // Substituição de valores nulos para não dar erro
            }
            jsonString += "]";
            
            StorageFolder dataFolder = await KnownFolders.MusicLibrary.CreateFolderAsync("JovemNerd", CreationCollisionOption.OpenIfExists);
            StorageFile nerdcastFile = await dataFolder.CreateFileAsync("nerdcast.json", CreationCollisionOption.ReplaceExisting);
            using (var fs = await nerdcastFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                var outStream = fs.GetOutputStreamAt(0);
                var dataWriter = new Windows.Storage.Streams.DataWriter(outStream);
                dataWriter.WriteString(jsonString);
                await dataWriter.StoreAsync();
                dataWriter.DetachStream();
                await outStream.FlushAsync();
                outStream.Dispose();
                fs.Dispose();
            }
            progressUpdate.Text = "Atualizado em " + DateTime.Now.ToLocalTime().ToString();
            PivotMainPage.SelectedItem = ListPivot;
            loadNerdcast();
        }
    }
}
