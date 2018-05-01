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

#if UNITY_WSA && !UNITY_EDITOR
        CreateMediaCapture();
        InitializeModel();
#else
        DisplayText("Does not work in player.");
#endif
    }

    private void DisplayText(string text)
    {
        textToDisplay = text;
        textToDisplayChanged = true;
    }

#if UNITY_WSA && !UNITY_EDITOR
    public async void InitializeModel()
    {
        StorageFile imageRecoModelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/image_recognition.onnx"));
        imageRecoModel = await Image_RecoModel.CreateImage_RecoModel(imageRecoModelFile);
    }

    public async void CreateMediaCapture()
    {
        MediaCapture = new MediaCapture();
        MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
        settings.StreamingCaptureMode = StreamingCaptureMode.Video;
        await MediaCapture.InitializeAsync(settings);

        CreateFrameReader();
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

        var mediaFrameReader = await MediaCapture.CreateFrameReaderAsync(colorFrameSource);
        mediaFrameReader.FrameArrived += MediaFrameReader_FrameArrived; ;
        await mediaFrameReader.StartAsync();
    }

    private async void MediaFrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        var now = DateTime.Now;
        if (lastPrediction == null || now.Subtract(lastPrediction) > predictEvery)
        {
            var frameReference = sender.TryAcquireLatestFrame();
            var videoFrame = frameReference.VideoMediaFrame.GetVideoFrame();
            Image_RecoModelOutput prediction = await imageRecoModel.EvaluateAsync(videoFrame);

            DisplayText(prediction.classLabel[0]);

            lastPrediction = now;
        }
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
