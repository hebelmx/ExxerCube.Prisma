# .NET 10 Migration Guide

## Current Status

**Project State**: ✅ .NET 9.0 - Fully functional with stable CSnakes 1.2.1

**Main Repository Requirement**: .NET 10.0 (shipping this week)

**Decision Timeline**: 1 day

---

## Option 1: Build CSnakes from Source (Recommended for .NET 10)

### Fork with .NET 10 Support
**Repository**: https://github.com/snickler/CSnakes/tree/dev/snickler/net10-upgrade

### Steps

1. **Clone CSnakes with .NET 10 support**:
   ```bash
   git clone -b dev/snickler/net10-upgrade https://github.com/snickler/CSnakes.git
   cd CSnakes
   ```

2. **Build CSnakes locally**:
   ```bash
   dotnet build
   dotnet pack
   ```

3. **Update to use local CSnakes**:

   Edit `Directory.Packages.props`:
   ```xml
   <ItemGroup>
     <!-- Use local CSnakes build for .NET 10 -->
     <PackageReference Include="CSnakes.Runtime" Version="2.0.0-*">
       <Source>F:\Dynamic\CSnakes\artifacts\packages\</Source>
     </PackageReference>
   </ItemGroup>
   ```

   Or add to `nuget.config`:
   ```xml
   <packageSources>
     <add key="Local CSnakes" value="F:\Dynamic\CSnakes\artifacts\packages\" />
   </packageSources>
   ```

4. **Update target framework**:

   Edit `Directory.Build.props`:
   ```xml
   <TargetFramework>net10.0</TargetFramework>
   ```

5. **Test**:
   ```bash
   dotnet build
   dotnet run --project ConsoleDemo
   ```

### Pros
- ✅ Native .NET 10 support
- ✅ Latest CSnakes features
- ✅ Aligns with main repository

### Cons
- ⚠️ Requires building from source
- ⚠️ Potential instability (dev branch)
- ⚠️ Needs retesting

### Risk Level
**Medium** - Dev branch, but actively maintained fork

---

## Option 2: Use Beta NuGet Package

### Package
**NuGet**: CSnakes.Runtime 2.0.0-beta.296

### Steps

1. **Update Directory.Packages.props**:
   ```xml
   <PackageVersion Include="CSnakes.Runtime" Version="2.0.0-beta.296" />
   ```

2. **Update Directory.Build.props**:
   ```xml
   <TargetFramework>net10.0</TargetFramework>
   ```

3. **Test thoroughly**:
   ```bash
   dotnet restore
   dotnet build
   dotnet test
   dotnet run --project ConsoleDemo
   ```

### Pros
- ✅ Easy to implement
- ✅ No source builds needed
- ✅ Standard NuGet workflow

### Cons
- ⚠️ Beta quality
- ⚠️ May have breaking changes
- ⚠️ Limited production testing

### Risk Level
**Medium-High** - Beta quality, less tested

---

## Option 3: Conditionally Target .NET 9/10

### Multi-Targeting Approach

1. **Update Directory.Build.props**:
   ```xml
   <PropertyGroup>
     <!-- Multi-target for compatibility -->
     <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
   </PropertyGroup>
   ```

2. **Conditional package references** in Directory.Packages.props:
   ```xml
   <ItemGroup>
     <!-- Stable for .NET 9 -->
     <PackageVersion Include="CSnakes.Runtime" Version="1.2.1" Condition="'$(TargetFramework)' == 'net9.0'" />

     <!-- Beta for .NET 10 -->
     <PackageVersion Include="CSnakes.Runtime" Version="2.0.0-beta.296" Condition="'$(TargetFramework)' == 'net10.0'" />
   </ItemGroup>
   ```

3. **Main repository uses .NET 10**, sample uses .NET 9

### Pros
- ✅ Sample stays stable on .NET 9
- ✅ Can upgrade when ready
- ✅ Low risk

### Cons
- ⚠️ Doesn't align with main repository (.NET 10)
- ⚠️ Multi-targeting complexity
- ⚠️ Delays .NET 10 adoption

### Risk Level
**Low** - Safe fallback, but delays migration

---

## Option 4: Wait for Stable CSnakes 2.0

### Timeline
Unknown - monitor https://github.com/tonybaloney/CSnakes/releases

### Pros
- ✅ Production-ready
- ✅ Fully tested
- ✅ Long-term stability

### Cons
- ❌ **NOT viable** - Main repo ships this week
- ❌ Unknown release date
- ❌ Blocks .NET 10 adoption

### Risk Level
**N/A** - Not viable for your timeline

---

## Recommendation

### For This Week's Shipping Deadline

**Use Option 1: Build from Source**

**Rationale**:
1. Only option that provides .NET 10 support this week
2. Fork is specifically for .NET 10 upgrade
3. You have local CSnakes repo available (F:\Dynamic\CSnakes)
4. Can test thoroughly before shipping
5. Can revert to stable release later

### Implementation Plan

**Day 1 (Today)**:
1. ✅ Build CSnakes from snickler/net10-upgrade branch
2. ✅ Update project to use local build
3. ✅ Run full test suite
4. ✅ Test CUDA functionality
5. ✅ Test device selection (CPU/GPU auto-switching)
6. ❌ If issues found → Fall back to Option 3 (multi-target)

**Fallback Strategy**:
- Keep .NET 9 working
- Document .NET 10 blockers
- Ship with .NET 9, upgrade post-release

---

## Testing Checklist

Before shipping on .NET 10:

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` all tests pass
- [ ] CSnakes source generation works
- [ ] Python environment initializes
- [ ] GOT-OCR2 model loads
- [ ] CPU execution works
- [ ] GPU detection works (if driver 581.80+)
- [ ] Device auto-selection works
- [ ] IndQuestResults integration works
- [ ] No regression vs .NET 9

---

## Rollback Plan

If .NET 10 fails:

1. **Revert to .NET 9**:
   ```bash
   git revert HEAD
   ```

2. **Multi-target approach**:
   - Main repo uses .NET 10
   - Sample uses .NET 9
   - Document migration path

3. **Document blockers** for future upgrade

---

## Contact & Resources

- **CSnakes .NET 10 Fork**: https://github.com/snickler/CSnakes/tree/dev/snickler/net10-upgrade
- **Main CSnakes**: https://github.com/tonybaloney/CSnakes
- **Local CSnakes**: F:\Dynamic\CSnakes

**Last Updated**: 2025-11-23
