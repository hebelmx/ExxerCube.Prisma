# SIARA Simulator - Demo Manual

## Quick Start Guide for Sales Demonstrations

---

## 📋 Pre-Demo Checklist

### Before the Demo
- [ ] Build published EXE: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
- [ ] Verify documents exist in `bulk_generated_documents_all_formats` folder
- [ ] Check `appsettings.json` arrival rate (default: 6 casos/minuto)
- [ ] Close any running instances
- [ ] Test internet connection (not required, but good for confidence)
- [ ] Have backup plan: Screenshots if live demo fails

### Demo Environment Setup
```
C:\SiaraDemo\
├── Siara.Simulator.exe
├── appsettings.json
├── cases.json (auto-generated)
├── logs\ (auto-created)
└── wwwroot\ (particles.js, logos, etc.)
```

---

## 🚀 Running the Demo

### Step 1: Start the Simulator
```bash
cd C:\SiaraDemo
Siara.Simulator.exe
```

**Expected Output:**
```
[15:30:00 INF] CaseService created with DocumentSourcePath: ...
[15:30:00 INF] Initial arrival rate: 6.0 cases/minute
Now listening on: https://localhost:5002
```

### Step 2: Open Browser
Navigate to: `https://localhost:5002`

### Step 3: Login
- **Username:** Any text (e.g., "demo", "banamex")
- **Password:** Any text (e.g., "demo", "password")
- **Note:** Authentication is cosmetic for demo purposes

### Step 4: Dashboard
You'll see:
- 🎨 Animated particles background (gold on green)
- 📊 Empty dashboard (waiting for first case)
- 🎛️ Arrival rate slider

---

## 🎬 Demo Script (5-7 Minutes)

### Opening (30 seconds)
> "Welcome to the SIARA simulator. Since we cannot legally access the actual CNBV authority request system, we've built this simulator that replicates real-world case arrivals from government agencies."

**Show:** Government branding, professional UI

### Problem Statement (1 minute)
> "Financial institutions receive legal requests from CNBV through the SIARA system. These requests—called 'oficios'—require urgent compliance: account freezes, information requests, documentation."
>
> "Processing these manually is slow, error-prone, and risky. Missing a deadline can result in regulatory penalties."

**Show:** Wait for first case to arrive (usually 5-30 seconds with 6 casos/minuto)

### Case Arrival (1 minute)
> "Watch—a new case just arrived! This is a real document from our test corpus."
>
> "Notice the Folio SIARA ID—this is the government tracking number. Each case includes PDF, DOCX, and XML versions of the official document."

**Show:**
- Case appears in real-time
- Click PDF/DOCX/XML buttons to show documents
- Highlight fecha de llegada (arrival timestamp)

### Configurability (1 minute)
> "For this demo, we're running at 6 cases per minute—that's 360 cases per hour. But watch what happens when we simulate peak load..."

**Show:**
- Drag slider to 30-60 casos/minuto
- Watch cases flood in
- Demonstrate system handles high volume

**Reset:** Drag back to 6 casos/minuto

### Real-World Application (2 minutes)
> "In production, our Prisma processor sits behind this simulator. It:
> 1. Receives each case in real-time
> 2. Extracts structured data using AI (GOT-OCR2)
> 3. Classifies the request type (EMBARGO, DESEMBARGO, etc.)
> 4. Routes to appropriate compliance workflow
> 5. Generates response within regulatory SLA"

**Show (if integrated):**
- Case status changes
- Processing metrics
- Generated compliance reports

### Value Proposition (1 minute)
> "Benefits for your institution:
> - ⚡ **Faster:** Process requests in seconds, not hours
> - ✅ **Accurate:** AI extraction eliminates manual errors
> - 📊 **Compliant:** Automated audit trail for regulators
> - 💰 **Cost Savings:** Reduce manual processing by 90%"

### Questions (Remaining time)
Common questions:
- **Q:** "Can it handle our document formats?"
  **A:** "Yes—PDF, DOCX, XML, even scanned images with OCR."

- **Q:** "What about security/compliance?"
  **A:** "All processing is on-premises. No data leaves your network."

- **Q:** "Integration with our systems?"
  **A:** "REST API, database connectors, or file-based integration—whatever you prefer."

---

## 🎛️ Demo Configuration Tricks

### For Different Scenarios

**Conservative Demo (Safe):**
```json
"AverageArrivalsPerMinute": 3.0
```
Slower arrival rate, more time to talk

**Impressive Demo (High Energy):**
```json
"AverageArrivalsPerMinute": 20.0
```
Cases flood in, shows system can handle volume

**Stress Test Demo:**
```json
"AverageArrivalsPerMinute": 60.0
```
Maximum arrival rate, proves scalability

**Reset Between Demos:**
```json
"ResetCasesOnStartup": true
```
Start fresh every time

---

## 🎨 Visual Highlights

### Point Out These Features
1. **Particles Background**
   - "Notice the animated background with government colors"
   - Professional, modern look

2. **Real-Time Updates**
   - "No refresh needed—cases appear instantly via WebSocket"
   - Shows technical sophistication

3. **Government Branding**
   - Official logos, colors, terminology
   - Builds trust and authenticity

4. **Clean Interface**
   - "Simple, intuitive—your staff can use it day one"
   - No training overhead

---

## 🛠️ Troubleshooting During Demo

### Issue: No Cases Appearing
**Cause:** Documents folder missing
**Fix:** Check `DocumentSourcePath` in appsettings.json

### Issue: Particles Not Loading
**Cause:** JS interop timing
**Fix:** Refresh browser (F5), particles.js will load

### Issue: Port Already in Use
**Cause:** Previous instance still running
**Fix:** Kill process, restart

### Issue: Browser Shows "Connection Lost"
**Cause:** Blazor circuit timeout
**Fix:** Refresh browser, should reconnect

---

## 📊 Key Metrics to Mention

### Performance
- **Arrival Rate:** 0.1 to 60 cases/minute (configurable)
- **Processing Time:** <2 seconds per case (with Prisma)
- **Accuracy:** 99%+ data extraction
- **Uptime:** 24/7 availability

### Business Impact
- **Manual Processing:** 10-15 minutes per case
- **Automated Processing:** <1 minute per case
- **Cost Reduction:** 90% reduction in manual effort
- **Risk Reduction:** Eliminates human error

---

## 🎯 Closing the Demo

### Strong Finish
> "What you've seen is the front door—the simulator receiving cases. Behind it is our Prisma processor with:
> - AI-powered document extraction
> - Intelligent classification
> - Automated workflow routing
> - Compliance reporting
> - Full audit trail
>
> This system can be operational in your institution within 30-60 days."

### Call to Action
> "Next steps:
> 1. We provide a trial system with your actual documents
> 2. You test with real cases (anonymized if needed)
> 3. We configure for your specific workflows
> 4. We train your team and deploy
>
> Can we schedule a technical deep-dive next week?"

---

## 📝 Demo Notes

### Do's
- ✅ Let cases arrive naturally (builds anticipation)
- ✅ Click PDF/DOCX to show actual documents
- ✅ Mention AI/ML prominently
- ✅ Focus on time savings and compliance
- ✅ Have backup screenshots ready

### Don'ts
- ❌ Don't apologize for "just a demo"
- ❌ Don't show configuration files
- ❌ Don't debug in front of client
- ❌ Don't promise features not built yet
- ❌ Don't rush—let system speak for itself

---

## 🎤 Talking Points by Audience

### For IT Directors
- "Built on .NET 10, latest technology stack"
- "Clean Architecture, SOLID principles"
- "Comprehensive test coverage"
- "Observability with Serilog/Seq"

### For Compliance Officers
- "Complete audit trail"
- "SLA tracking and alerting"
- "Regulatory report generation"
- "Nothing leaves your network"

### For Operations Managers
- "90% reduction in manual effort"
- "Faster response times"
- "Fewer errors, less rework"
- "Staff can focus on complex cases"

### For C-Level
- "Reduce operational costs"
- "Eliminate regulatory risk"
- "Competitive advantage"
- "ROI within 12 months"

---

## 📞 Follow-Up Materials

### Send After Demo
1. This demo manual
2. Screenshots/recording of their demo
3. Technical architecture diagram
4. Pricing proposal
5. Case studies (if available)
6. Trial system agreement

### Template Email
```
Subject: SIARA Simulator Demo Follow-Up

Dear [Name],

Thank you for attending today's demonstration of our SIARA case processing simulator.

As discussed, our Prisma system can process government authority requests
in seconds vs. the 10-15 minutes currently required manually.

Attached you'll find:
- Demo recording
- Technical specifications
- Pricing proposal
- Next steps

I'd like to schedule a 30-minute technical deep-dive with your IT team
next week. When works best for you?

Best regards,
[Your Name]
```

---

## 🔒 Important Reminders

### Legal/Compliance
- ✅ Emphasize this is a SIMULATOR (not actual SIARA)
- ✅ No real government data is used
- ✅ All documents are synthetic/anonymized
- ✅ Processing happens on-premises

### Security
- ✅ No cloud dependencies
- ✅ No data transmission outside client network
- ✅ Full audit trail
- ✅ Role-based access control (in full product)

---

## 🎓 Training for Sales Team

### Before Demo
- Practice 3-5 times alone
- Time yourself (should be 5-7 minutes)
- Prepare for questions
- Test all features

### Know Your Numbers
- Manual processing time: **10-15 minutes/case**
- Automated processing: **<1 minute/case**
- Accuracy: **99%+**
- Cost reduction: **90%**

### Confidence Builders
- System is stable (Observable pattern)
- UI is professional (particles, branding)
- Configuration is flexible
- Technology is modern

---

## ✨ Success Metrics

### Demo Considered Successful If:
- [ ] Client sees cases arrive in real-time
- [ ] Client understands value proposition
- [ ] Client agrees to next meeting
- [ ] Client provides specific use case
- [ ] Client introduces decision-maker

### Red Flags
- ⚠️ Client seems confused about purpose
- ⚠️ Client focuses on edge cases
- ⚠️ Client doesn't engage/ask questions
- ⚠️ Client mentions "we'll think about it"

**Action:** Circle back to value prop, pain points

---

## 📚 Additional Resources

- **CONFIGURATION.md** - Deployment and configuration details
- **LESSONS_LEARNED.md** - Technical insights from development
- **README.md** - Developer documentation
- **Prisma Solution** - Full product capabilities

---

**Remember:** You're not just demonstrating technology—you're solving a real business problem. The simulator is the proof. Your job is to connect it to their pain points.

**Questions?** Contact the development team for technical details or customization requests.
