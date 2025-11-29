using CSnakes.Runtime.Python;
using TransformersSharp.Tokenizers;

namespace TransformersSharp.Pipelines
{
    /// <summary>
    /// Base class for transformer pipelines.
    /// </summary>
    public class Pipeline
    {
        /// <summary>
        /// Gets the device type the pipeline is running on.
        /// </summary>
        public string DeviceType { get; private set; }

        internal PyObject PipelineObject { get; }

        internal Pipeline(PyObject pipelineObject)
        {
            PipelineObject = pipelineObject;
            DeviceType = pipelineObject.GetAttr("device").ToString();
        }

        internal IReadOnlyList<IReadOnlyDictionary<string, PyObject>> RunPipeline(string input)
        {
            return TransformerEnvironment.TransformersWrapper.CallPipeline(PipelineObject, input);
        }

        internal IReadOnlyList<IReadOnlyDictionary<string, PyObject>> RunPipeline(IReadOnlyList<string> inputs)
        {
            return TransformerEnvironment.TransformersWrapper.CallPipelineWithList(PipelineObject, inputs);
        }

        private PreTrainedTokenizerBase? _tokenizer = null;
        
        /// <summary>
        /// Gets the tokenizer associated with this pipeline.
        /// </summary>
        public PreTrainedTokenizerBase Tokenizer
        {
            get
            {
                _tokenizer ??= new PreTrainedTokenizerBase(PipelineObject.GetAttr("tokenizer"));
                return _tokenizer;
            }
        }
    }
}
