using TransformersSharp.Pipelines;
using TransformersSharp.Tokenizers;
using Shouldly;
using Xunit;

namespace TransformersSharp.Tests
{
    /// <summary>
    /// Tests for the TransformerEnvironment functionality.
    /// </summary>
    public class TransformerEnvironmentTest
    {
        /// <summary>
        /// Tests that the pipeline creation returns a valid pipeline object.
        /// </summary>
        [Fact]
        public void Pipeline_ShouldReturnPipelineObject()
        {
            var pipeline = TransformerEnvironment.Pipeline("text-classification", "distilbert-base-uncased-finetuned-sst-2-english");
            pipeline.ShouldNotBeNull();
            pipeline.ShouldBeOfType<Pipeline>();

            pipeline.DeviceType.ShouldBe("cpu");
        }

        /// <summary>
        /// Tests that text classification pipeline can classify text correctly.
        /// </summary>
        [Fact]
        public void Pipeline_Classify()
        {
            var pipeline = TextClassificationPipeline.FromModel("distilbert-base-uncased-finetuned-sst-2-english");
            var result = pipeline.Classify("I love programming!");
            result.ShouldHaveSingleItem();
            result[0].Label.ShouldBe("POSITIVE");
            ((double)result[0].Score).ShouldBeInRange(0.0, 1.0);
        }

        /// <summary>
        /// Tests that text classification pipeline can classify multiple texts in batch.
        /// </summary>
        [Fact]
        public void Pipeline_ClassifyBatch()
        {
            var pipeline = TextClassificationPipeline.FromModel("distilbert-base-uncased-finetuned-sst-2-english");
            var inputs = new List<string> { "I love programming!", "I hate bugs!" };
            var results = pipeline.ClassifyBatch(inputs);
            results.Count.ShouldBe(2);
            results[0].Label.ShouldBe("POSITIVE");
            ((double)results[0].Score).ShouldBeInRange(0.0, 1.0);
            results[1].Label.ShouldBe("NEGATIVE");
            ((double)results[1].Score).ShouldBeInRange(0.0, 1.0);
        }

        /// <summary>
        /// Tests that text generation pipeline can generate text correctly.
        /// </summary>
        [Fact]
        public void Pipeline_TextGeneration()
        {
            var pipeline = TextGenerationPipeline.FromModel("facebook/opt-125m");
            var result = pipeline.Generate("How many helicopters can a human eat in one sitting?");
            result.ShouldHaveSingleItem();
            result.First().ToLowerInvariant().ShouldContain("helicopter");
        }

        /// <summary>
        /// Tests that tokenization works correctly from text generation pipeline.
        /// </summary>
        [Fact]
        public void Pipeline_TokenizeFromTextGenerationPipeline()
        {
            var pipeline = TextGenerationPipeline.FromModel("facebook/opt-125m");
            var InputIds = pipeline.Tokenizer.Tokenize("How many helicopters can a human eat in one sitting?");
            InputIds.Length.ShouldBe(12);
            InputIds[0].ShouldBe(2);
        }

        /// <summary>
        /// Tests that tokenization works correctly from pretrained tokenizer.
        /// </summary>
        [Fact]
        public void Pipeline_TokenizeFromPretrained()
        {
            var tokenizer = PreTrainedTokenizerBase.FromPretrained("facebook/opt-125m");
            var InputIds = tokenizer.Tokenize("How many helicopters can a human eat in one sitting?");
            InputIds.Length.ShouldBe(12);
            InputIds[0].ShouldBe(2);
        }

        /// <summary>
        /// Tests that tokenizer encoding works correctly.
        /// </summary>
        [Fact]
        public void Tokenizer_Encode()
        {
            var tokenizer = PreTrainedTokenizerBase.FromPretrained("facebook/opt-125m");
            var input = "How many helicopters can a human eat in one sitting?";
            var tokens = tokenizer.EncodeToTokens(input, out string? normalizedText);
            tokens.ShouldNotBeNull();
            tokens.ShouldNotBeEmpty();
            normalizedText.ShouldBeNull();

            tokens[1].Id.ShouldBe(6179);
            tokens[1].Value.ShouldBe("How");

            tokens[tokens.Count - 1].Value.ShouldBe("?");
            tokens[tokens.Count - 1].Id.ShouldBe(116);
        }

        /// <summary>
        /// Tests that tokenizer decoding works correctly.
        /// </summary>
        [Fact]
        public void Tokenizer_Decode()
        {
            var tokenizer = PreTrainedTokenizerBase.FromPretrained("facebook/opt-125m", addSpecialTokens: false);
            var input = "How many helicopters can a human eat in one sitting?";
            var tokens = tokenizer.EncodeToTokens(input, out string? normalizedText);
            tokens.ShouldNotBeNull();
            tokens.ShouldNotBeEmpty();
            normalizedText.ShouldBeNull();
            string decodedText = tokenizer.Decode(tokens.Select(et => et.Id));
            decodedText.ShouldBe(input);
        }

        /// <summary>
        /// Tests that image classification pipeline can classify images from URL correctly.
        /// </summary>
        [Fact]
        public void ImageClassificationPipeline_ClassifyUrl()
        {
            var pipeline = ImageClassificationPipeline.FromModel("google/mobilenet_v2_1.0_224");
            var imagePath = "https://huggingface.co/datasets/Narsil/image_dummy/raw/main/parrots.png"; // Replace with a valid image path
            var result = pipeline.Classify(imagePath);
            result.ShouldNotBeNull();
            result.ShouldNotBeEmpty();
            ((double)result.First().Score).ShouldBeInRange(0.5, 1.0);
            result.First().Label.ShouldBe("hornbill");
        }

        /// <summary>
        /// Tests that object detection pipeline can detect objects from URL correctly.
        /// </summary>
        [Fact]
        public void ObjectDetectionPipeline_DetectUrl()
        {
            var pipeline = ObjectDetectionPipeline.FromModel("facebook/detr-resnet-50");
            var imagePath = "https://huggingface.co/datasets/Narsil/image_dummy/raw/main/parrots.png"; // Replace with a valid image path
            var result = pipeline.Detect(imagePath).ToArray();
            result.ShouldNotBeNull();
            result.ShouldNotBeEmpty();
            ((double)result[0].Score).ShouldBeInRange(0.5, 1.0);
            result[0].Label.ShouldBe("bird");
            var box = result[0].Box;
            box.XMin.ShouldBeInRange(0, box.XMax);
            box.YMin.ShouldBeInRange(0, box.YMax);
            box.XMax.ShouldBeInRange(box.XMin, 400);
            box.YMax.ShouldBeInRange(box.YMin, 600);
        }

        /// <summary>
        /// Tests that text to audio pipeline can generate audio correctly.
        /// </summary>
        [Fact]
        public void TextToAudioPipeline_Generate()
        {
            var pipeline = TextToAudioPipeline.FromModel("suno/bark-small");
            var text = "Hello, this is a test.";
            var audioResult = pipeline.Generate(text);
            audioResult.Audio.IsEmpty.ShouldBeFalse();
            audioResult.Audio.Length.ShouldBeGreaterThan(0);

            audioResult.SamplingRate.ShouldBe(24000);
        }

        /// <summary>
        /// Tests that automatic speech recognition pipeline can transcribe audio correctly.
        /// </summary>
        [Fact]
        public void AutomaticSpeechRecognitionPipeline_Transcribe()
        {
            var pipeline = AutomaticSpeechRecognitionPipeline.FromModel("openai/whisper-tiny");
            var audioPath = "https://huggingface.co/datasets/Narsil/asr_dummy/resolve/main/1.flac";
            var result = pipeline.Transcribe(audioPath);
            result.ShouldNotBeNull();
            result.ShouldNotBeEmpty();
            result.ToLowerInvariant().ShouldContain("stew for dinner");
        }
    }
}