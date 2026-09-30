#!/bin/bash
# Micro-Task 1.2: Create EF Core Configuration for ProcessingQueue
# Duration: 60 minutes
# Risk Level: LOW
# Dependencies: Micro-Task 1.1
# Rollback Time: 5 minutes

set -e

echo "🔧 Micro-Task 1.2: Create EF Core Configuration for ProcessingQueue"

# Step 1: Create configuration directory
echo "📁 Creating configuration directory..."
mkdir -p "code/src/Infrastructure/ExxerAI.Infrastructure/Persistence/Configurations"

# Step 2: Create configuration file
echo "📝 Creating EF Core configuration..."
cat > "code/src/Infrastructure/ExxerAI.Infrastructure/Persistence/Configurations/ProcessingQueueConfiguration.cs" << 'EOF'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ExxerAI.Domain.Entities;
using System.Text.Json;

namespace ExxerAI.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for ProcessingQueue entity
/// </summary>
public class ProcessingQueueConfiguration : IEntityTypeConfiguration<ProcessingQueue>
{
    public void Configure(EntityTypeBuilder<ProcessingQueue> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .IsRequired()
            .HasMaxLength(255);
            
        builder.Property(x => x.DocumentId)
            .IsRequired()
            .HasMaxLength(255);
            
        builder.Property(x => x.ChangeEventId)
            .HasMaxLength(255);
            
        builder.Property(x => x.QueueName)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(x => x.WorkerId)
            .HasMaxLength(255);
            
        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);
            
        builder.Property(x => x.ProgressDescription)
            .HasMaxLength(500);
            
        builder.Property(x => x.ProcessingConfig)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonSerializerOptions.Default) ?? new());
                
        builder.Property(x => x.ProcessingResults)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonSerializerOptions.Default) ?? new());
                
        builder.Property(x => x.Metrics)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonSerializerOptions.Default) ?? new());
                
        builder.Property(x => x.StatusQueueItem)
            .HasConversion<string>();
            
        builder.HasOne(x => x.ChangeEvent)
            .WithMany()
            .HasForeignKey(x => x.ChangeEventId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
EOF

# Step 3: Validate file creation
echo "🔍 Validating file creation..."
if [ ! -f "code/src/Infrastructure/ExxerAI.Infrastructure/Persistence/Configurations/ProcessingQueueConfiguration.cs" ]; then
    echo "❌ Configuration file not created"
    exit 1
fi

# Step 4: Validate compilation
echo "🔍 Validating compilation..."
if ! dotnet build "code/src/Infrastructure/ExxerAI.Infrastructure/ExxerAI.Infrastructure.csproj" --verbosity quiet >/dev/null 2>&1; then
    echo "❌ Compilation failed"
    exit 1
fi

echo "✅ Micro-Task 1.2 completed successfully"
echo "📊 Summary:"
echo "   - Configuration directory created"
echo "   - ProcessingQueueConfiguration.cs created"
echo "   - Compilation verified"
echo "   - Ready for DbContext registration"

