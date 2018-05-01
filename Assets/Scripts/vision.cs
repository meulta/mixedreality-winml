 #if UNITY_WSA && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Storage;
using Windows.AI.MachineLearning.Preview;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Diagnostics;

// Image_Reco

public sealed class Image_RecoModelInput
{
    public VideoFrame data { get; set; }
}

public sealed class Image_RecoModelOutput
{
    public IList<string> classLabel { get; set; }
    public IDictionary<string, float> loss { get; set; }
    public Image_RecoModelOutput()
    {
        this.classLabel = new List<string>();
        this.loss = new Dictionary<string, float>()
            {
                { "fork", 0f },
                { "hand_empty", 0f },
                {"hand_holding_fork", 0f },
                {"hand_holding_mug", 0f },
                {"hand_holding_plate", 0f },
                {"hand_holding_water_bottle", 0f },
                {"mug", 0f },
                {"plate", 0f },
                {"water_bottle", 0f }
            };
    }
}

public sealed class Image_RecoModel
{
    private LearningModelPreview learningModel;

    public static async Task<Image_RecoModel> CreateImage_RecoModel(StorageFile file)
    {
        LearningModelPreview learningModel = await LearningModelPreview.LoadModelFromStorageFileAsync(file);
        Image_RecoModel model = new Image_RecoModel();
        model.learningModel = learningModel;
        return model;
    }

    public async Task<Image_RecoModelOutput> EvaluateAsync(StorageFile inputFile)
    {
        using (VideoFrame inputFrame = await this.ConvertFileToVideoFrameAsync(inputFile))
        {
            return await EvaluateAsync(inputFrame);
        }
    }

    public async Task<Image_RecoModelOutput> EvaluateAsync(VideoFrame frame)
    {
        Image_RecoModelOutput output = new Image_RecoModelOutput();

        LearningModelBindingPreview binding = new LearningModelBindingPreview(learningModel);

        binding.Bind("data", frame);
        binding.Bind("classLabel", output.classLabel);
        binding.Bind("loss", output.loss);

        LearningModelEvaluationResultPreview evalResult = await learningModel.EvaluateAsync(binding, string.Empty);

        return output;
    }

    private async Task<VideoFrame> ConvertFileToVideoFrameAsync(StorageFile file)
    {
        SoftwareBitmap softwareBitmap;
        using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
        {
            // Create the decoder from the stream 
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            // Get the SoftwareBitmap representation of the file 
            softwareBitmap = await decoder.GetSoftwareBitmapAsync();
            softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
        }

        return VideoFrame.CreateWithSoftwareBitmap(softwareBitmap);
    }
}
#endif