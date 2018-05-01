using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Windows.Speech;

#if UNITY_WSA && !UNITY_EDITOR
using Windows.Storage;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.Storage.FileProperties;
using Windows.Foundation;
using Windows.Media.Capture.Frames;
using System.Diagnostics;
#endif

public class SceneStartup : MonoBehaviour
{

    Dictionary<string, System.Action> keywords;
    public GameObject Label;
    private TextMesh LabelText;
    KeywordRecognizer keywordRecognizer;
    DateTime lastPrediction;
    TimeSpan predictEvery = TimeSpan.FromMilliseconds(500);
    string textToDisplay;
    bool textToDisplayChanged;

#if UNITY_WSA && !UNITY_EDITOR
    Image_RecoModel imageRecoModel;
    MediaCapture MediaCapture;
#endif

    void Start()
    {
        LabelText = Label.GetComponent<TextMesh>();

        keywords = new Dictionary<string, Action>();
        keywords.Add("scan", () =>
        {
            LabelText.text = "Scanning...";
            Predict();
        });
        keywordRecognizer = new KeywordRecognizer(keywords.Keys.ToArray());
        keywordRecognizer.OnPhraseRecognized += KeywordRecognizer_OnPhraseRecognized;
        keywordRecognizer.Start();

#if UNITY_WSA && !UNITY_EDITOR
        CreateMediaCapture();
#endif

        InitializeModel();
    }

    public async void InitializeModel()
    {
#if UNITY_WSA && !UNITY_EDITOR
        StorageFile imageRecoModelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/image_recognition.onnx"));
        imageRecoModel = await Image_RecoModel.CreateImage_RecoModel(imageRecoModelFile);
#endif
    }

    private void DisplayText(string text)
    {
        textToDisplay = text;
        textToDisplayChanged = true;
    }

    public async void Predict()
    {
#if UNITY_WSA && !UNITY_EDITOR
        StorageFile file = await GetPreviewFrame();
        Image_RecoModelOutput prediction = await imageRecoModel.EvaluateAsync(file);
        LabelText.text = prediction.classLabel[0];

        keywordRecognizer.Stop();
        keywordRecognizer.Start();
#endif
    }

    private void KeywordRecognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        Action keywordAction;
        if (keywords.TryGetValue(args.text, out keywordAction))
        {
            keywordAction.Invoke();
        }
    }

#if UNITY_WSA && !UNITY_EDITOR
    public async void CreateMediaCapture()
    {
        MediaCapture = new MediaCapture();
        MediaCapture.Failed += MediaCapture_Failed;
        MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
        settings.StreamingCaptureMode = StreamingCaptureMode.Video;
        await MediaCapture.InitializeAsync(settings);

        CreateFrameReader();
    }

    private void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
    {
    }

    private async void CreateFrameReader()
    {
        var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();

        MediaFrameSourceGroup selectedGroup = null;
        MediaFrameSourceInfo colorSourceInfo = null;

        foreach (var sourceGroup in frameSourceGroups)
        {
            foreach (var sourceInfo in sourceGroup.SourceInfos)
            {
                if (sourceInfo.MediaStreamType == MediaStreamType.VideoPreview
                    && sourceInfo.SourceKind == MediaFrameSourceKind.Color)
                {
                    colorSourceInfo = sourceInfo;
                    break;
                }
            }
            if (colorSourceInfo != null)
            {
                selectedGroup = sourceGroup;
                break;
            }
        }

        var colorFrameSource = MediaCapture.FrameSources[colorSourceInfo.Id];
        var preferredFormat = colorFrameSource.SupportedFormats.Where(format =>
        {
            return format.Subtype == MediaEncodingSubtypes.Argb32;

        }).FirstOrDefault();

        //if (preferredFormat == null)
        //{
        //    // Our desired format is not supported
        //    return;
        //}

        //await colorFrameSource.SetFormatAsync(preferredFormat);

        var mediaFrameReader = await MediaCapture.CreateFrameReaderAsync(colorFrameSource);
        mediaFrameReader.FrameArrived += MediaFrameReader_FrameArrived; ;
        await mediaFrameReader.StartAsync();
    }

    private async void MediaFrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        var now = DateTime.Now;
        if (lastPrediction == null || now.Subtract(lastPrediction) > predictEvery)
        {
            Stopwatch videosw = new Stopwatch();
            videosw.Start();
            var frameReference = sender.TryAcquireLatestFrame();
            var videoFrame = frameReference.VideoMediaFrame.GetVideoFrame();
            videosw.Stop();

            Stopwatch predictsw = new Stopwatch();
            predictsw.Start();
            Image_RecoModelOutput prediction = await imageRecoModel.EvaluateAsync(videoFrame);
            predictsw.Stop();


            DisplayText(prediction.classLabel[0]);

            lastPrediction = now;
        }
    }

    private async Task<StorageFile> GetPreviewFrame()
    {
        var myPictures = await StorageLibrary.GetLibraryAsync(Windows.Storage.KnownLibraryId.Pictures);
        StorageFile file = await myPictures.SaveFolder.CreateFileAsync("photo.jpg", CreationCollisionOption.GenerateUniqueName);

        using (var captureStream = new InMemoryRandomAccessStream())
        {
            await MediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), captureStream);

            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var decoder = await BitmapDecoder.CreateAsync(captureStream);
                var encoder = await BitmapEncoder.CreateForTranscodingAsync(fileStream, decoder);

                var properties = new BitmapPropertySet {
            { "System.Photo.Orientation", new BitmapTypedValue(PhotoOrientation.Normal, PropertyType.UInt16) }
        };
                await encoder.BitmapProperties.SetPropertiesAsync(properties);

                await encoder.FlushAsync();
            }
        }

        return file;
    }
#endif

    void Update()
    {
        if (textToDisplayChanged)
        {
            LabelText.text = textToDisplay;
            textToDisplayChanged = false;
        }
    }
}
