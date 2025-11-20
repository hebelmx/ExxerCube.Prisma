// Global using directives for ExxerAI.RealTimeCommunication.Server.Tests

// XUnit v2 Testing Framework (Stryker Compatible)
global using Xunit;

// Shouldly Assertions
global using Shouldly;

// NSubstitute Mocking (for legacy tests)
global using NSubstitute;
global using NSubstitute.ExceptionExtensions;

// Railway-Oriented Programming
global using IndQuestResults;

// ASP.NET Core Testing
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.AspNetCore.SignalR.Client;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

// ExxerAI Domain
// TODO: Update these usings when hub implementations are created in Phase 4
// global using ExxerAI.RealTimeCommunication.Adapters.SignalR;
// global using ExxerAI.RealTimeCommunication.Models;
// global using ExxerAI.RealTimeCommunication.Abstractions;

// ExxerCube SignalR Abstractions
global using ExxerCube.Prisma.SignalR.Abstractions.Abstractions.Hubs;
