// Global using directives for ExxerAI.Helix
global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Linq;
global using System.Text;
global using System.Text.Json;
global using System.Threading;
global using System.Threading.Tasks;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;

global using ExxerAI.Domain.Common;
global using ExxerAI.Application.Common;
global using ExxerAI.Application.Interfaces;
global using ExxerAI.Application.Ports;
global using ExxerAI.Domain.Entities;
global using ExxerAI.Domain.DocumentProcessing;
global using ExxerAI.Domain.BusinessIntelligence;
global using ExxerAI.Domain.BusinessIntelligence.Kpis;
global using ExxerAI.Axis.Abstractions;
global using ExxerAI.Vault.Abstractions;
global using ExxerAI.Cortex.AI;
global using ExxerAI.Domain.CubeXplorer.Interfaces.ImageProcessing;
global using ExxerAI.Domain.CubeXplorer.Models.ImageProcessing;
global using ExxerAi.Axioms.Models.Configuration;
global using ExxerAI.Axioms.Models.Storage;

// Result pattern types
global using IndQuestResults;
global using IndQuestResults.Operations;

// Image processing
global using SixLabors.ImageSharp;
global using SixLabors.ImageSharp.Processing;
global using SixLabors.ImageSharp.PixelFormats;
global using SixLabors.ImageSharp.Drawing.Processing;

// Database and vector stores
global using Neo4jClient;
global using Qdrant.Client;
global using Qdrant.Client.Grpc;
global using Pgvector;
global using Pgvector.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore;

// Application services
global using ExxerAI.Application.TechnicalServices;

// OCR
global using Tesseract;