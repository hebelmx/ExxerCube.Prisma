using CSnakes.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TransformersSharp.Pipelines;

namespace TransformersSharp
{
    /// <summary>
    /// Provides a static environment for transformer models and utilities.
    /// </summary>
    public static class TransformerEnvironment
    {
        private static readonly IPythonEnvironment? _env;
        private static readonly Lock _setupLock = new();

        static TransformerEnvironment()
        {
            lock (_setupLock)
            {
                try
                {
                    IHostBuilder builder = Host.CreateDefaultBuilder()
                        .ConfigureServices(services =>
                        {
                            // Use Local AppData folder for Python installation
                            string appDataPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TransformersSharp");

                            // Create the directory if it doesn't exist
                            if (!Directory.Exists(appDataPath))
                                Directory.CreateDirectory(appDataPath);

                            // If user has an environment variable TRANSFORMERS_SHARP_VENV_PATH, use that instead
                            string? envPath = Environment.GetEnvironmentVariable("TRANSFORMERS_SHARP_VENV_PATH");
                            string venvPath;
                            if (envPath != null)
                                venvPath = envPath;
                            else
                                venvPath = Path.Join(appDataPath, "venv");

                            // Write requirements to appDataPath
                            string requirementsPath = Path.Join(appDataPath, "requirements.txt");

                            // TODO: Make this configurable
                            string[] requirements =
                            {
                                "transformers",
                                "sentence_transformers",
                                "torch",
                                "pillow",
                                "timm",
                                "einops"
                            };

                            File.WriteAllText(requirementsPath, string.Join('\n', requirements));

                            // CSnakes DI configuration for .NET 10
                            // CSnakes 2.0.0-beta.265 defaults to Python 3.12 for .NET 10 compatibility
                            services
                                    .WithPython()
                                    .WithHome(appDataPath)
                                    .WithVirtualEnvironment(venvPath)
                                    .WithUvInstaller()
                                    .FromRedistributable(); // Download Python 3.12 automatically (default in CSnakes 2.0.0-beta)
                        });

                    var app = builder.Build();

                    _env = app.Services.GetRequiredService<IPythonEnvironment>();
                }
                catch (Exception ex)
                {
                    // Log initialization error - CSnakes will handle Python download/installation
                    System.Diagnostics.Debug.WriteLine($"TransformersSharp environment initialization error: {ex.Message}");
                    throw;
                }
            }
        }

        private static IPythonEnvironment Env => _env ?? throw new InvalidOperationException("Python environment is not initialized..");

        internal static ITransformersWrapper TransformersWrapper => Env.TransformersWrapper();
        internal static ISentenceTransformersWrapper SentenceTransformersWrapper => Env.SentenceTransformersWrapper();

        /// <summary>
        /// Login to Huggingface with a token.
        /// </summary>
        /// <param name="token"></param>
        public static void Login(string token)
        {
            var wrapperModule = Env.TransformersWrapper();
            wrapperModule.HuggingfaceLogin(token);
        }

        /// <summary>
        /// Creates a new pipeline for the specified task and model.
        /// </summary>
        /// <param name="task">The task to perform (e.g., "text-generation", "image-classification").</param>
        /// <param name="model">The model to use for the pipeline.</param>
        /// <param name="tokenizer">The tokenizer to use for the pipeline.</param>
        /// <param name="torchDtype">The torch data type to use.</param>
        /// <returns>A new pipeline instance.</returns>
        public static Pipeline Pipeline(string? task = null, string? model = null, string? tokenizer = null, TorchDtype? torchDtype = null)
        {
            var wrapperModule = Env.TransformersWrapper();
            string? torchDtypeStr = torchDtype?.ToString() ?? null;
            var pipeline = wrapperModule.Pipeline(task, model, tokenizer, torchDtypeStr);

            return new Pipeline(pipeline);
        }

        /// <summary>
        /// Disposes the transformer environment and releases resources.
        /// </summary>
        public static void Dispose()
        {
            _env?.Dispose();
        }
    }
}
