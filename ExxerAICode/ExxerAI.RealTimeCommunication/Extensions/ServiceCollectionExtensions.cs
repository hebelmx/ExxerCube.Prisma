namespace ExxerAI.RealTimeCommunication.Extensions
{
    /// <summary>
    /// Service collection extensions for real-time communication
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds real-time communication services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configuration">The configuration instance</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddRealTimeCommunication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
        
            // Configure options
            services.Configure<RealTimeCommunicationOptions>(config =>
            {
                configuration.GetSection("RealTimeCommunication").Bind(config);
            });

            // Add SignalR services
            services.AddSignalR(options =>
            {
                // Enhanced IndTrace pattern: Improved SignalR configuration
                options.EnableDetailedErrors = true;
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            });

            // Register hubs as scoped (per request/connection)
            services.AddScoped<SystemHub>();
            services.AddScoped<AgentHub>();
            services.AddScoped<TaskHub>();
            services.AddScoped<DocumentHub>();
            services.AddScoped<EconomicHub>();

            // Register adapter as singleton for performance and state management
            services.AddSingleton<SignalRAdapter>();

            // Register port interfaces as singletons (delegate to same singleton adapter)
            services.AddSingleton<IRealTimeCommunicationPort>(provider => provider.GetRequiredService<SignalRAdapter>());
            services.AddSingleton<IEventBroadcastingPort>(provider => provider.GetRequiredService<SignalRAdapter>());
            services.AddSingleton<INotificationPort>(provider => provider.GetRequiredService<SignalRAdapter>());
            services.AddSingleton<ICompleteCommunicationPort>(provider => provider.GetRequiredService<SignalRAdapter>());

            return services;
        }

        /// <summary>
        /// Adds real-time communication services with custom options
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureOptions">Action to configure options</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddRealTimeCommunication(
            this IServiceCollection services,
            Action<RealTimeCommunicationOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureOptions);
        
            // Configure options
            services.Configure(configureOptions);

            // Add SignalR services with default configuration
            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            });

            // Register services
            RegisterCoreServices(services);

            return services;
        }

        /// <summary>
        /// Adds real-time communication services with custom SignalR and communication options
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configureSignalR">Action to configure SignalR options</param>
        /// <param name="configureOptions">Action to configure communication options</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddRealTimeCommunication(
            this IServiceCollection services,
            Action<HubOptions> configureSignalR,
            Action<RealTimeCommunicationOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(configureSignalR);
            ArgumentNullException.ThrowIfNull(configureOptions);
        
            // Configure options
            services.Configure(configureOptions);

            // Add SignalR services with custom configuration
            services.AddSignalR(configureSignalR);

            // Register services
            RegisterCoreServices(services);

            return services;
        }


        /// <summary>
        /// Registers core services for real-time communication
        /// </summary>
        /// <param name="services">The service collection</param>
        private static void RegisterCoreServices(IServiceCollection services)
        {
            // Register hubs as scoped (per request/connection)
            services.AddScoped<SystemHub>();
            services.AddScoped<AgentHub>();
            services.AddScoped<TaskHub>();
            services.AddScoped<DocumentHub>();
            services.AddScoped<EconomicHub>();

            // Register adapter as singleton for performance and state management
            services.AddSingleton<SignalRAdapter>();

            // Register port interfaces as singletons (delegate to same singleton adapter)
            services.AddSingleton<IRealTimeCommunicationPort>(provider => provider.GetRequiredService<SignalRAdapter>());
            services.AddSingleton<IEventBroadcastingPort>(provider => provider.GetRequiredService<SignalRAdapter>());
            services.AddSingleton<INotificationPort>(provider => provider.GetRequiredService<SignalRAdapter>());
            services.AddSingleton<ICompleteCommunicationPort>(provider => provider.GetRequiredService<SignalRAdapter>());
        }
    }
}