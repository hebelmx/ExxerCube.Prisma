// Global using directives for Infrastructure.Database Tests

global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using ExxerCube.Prisma.Domain;
global using ExxerCube.Prisma.Domain.Entities;
global using ExxerCube.Prisma.Domain.Interfaces;
global using ExxerCube.Prisma.Infrastructure.Database;
global using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
global using ExxerCube.Prisma.Infrastructure.Database.Repositories;
global using ExxerCube.Prisma.Infrastructure.Database.Metrics;
global using ExxerCube.Prisma.Testing.Abstractions;
global using ExxerCube.Prisma.Testing.Infrastructure;
global using ExxerCube.Prisma.Testing.Infrastructure.TestData;
global using ExxerCube.Prisma.Testing.Contracts;
global using IndQuestResults;
global using IndQuestResults.Async;
global using IndQuestResults.Operations;
global using Meziantou.Extensions.Logging.Xunit.v3;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore.InMemory;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using System.Diagnostics;
global using System.Linq.Expressions;
global using ExxerCube.Prisma.Domain.Enum;
global using ExxerCube.Prisma.Domain.Models;
global using ExxerCube.Prisma.Domain.ValueObjects;
global using NSubstitute;
global using Shouldly;
global using Xunit;

