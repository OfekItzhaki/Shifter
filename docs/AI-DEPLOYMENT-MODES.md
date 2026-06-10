# AI Deployment Modes

Shifter must support both SaaS customers and organizations that require their
data to stay inside their own infrastructure. AI features therefore must be
configured as a deployment choice, not hard-coded to one vendor.

## Recommended Contract

Use an OpenAI-compatible HTTP endpoint for chat/import assistant features:

```env
AI__ApiKey=...
AI__Model=...
AI__BaseUrl=https://api.openai.com/v1
```

The API key may be empty only for local endpoints that do not require auth.
The app should continue to run with `NoOpAiAssistant` when AI is disabled.

## Modes

### SaaS Default

Use Shifter-owned AI credentials for the hosted product.

Good for:
- Fast onboarding.
- Best model quality.
- Lowest customer setup burden.

Requirements:
- Product terms must disclose that user prompts are sent to the configured AI
  processor.
- Avoid sending unnecessary personal/schedule data.
- Keep support escalation separate from AI responses.

### Customer-Managed Cloud AI

Use the customer's own AI provider credentials and endpoint, for example a
customer Azure OpenAI deployment or another OpenAI-compatible gateway.

Good for:
- Enterprise procurement.
- Customer-controlled billing and data-processing agreement.
- Private networking options when hosted in the customer's cloud.

Requirements:
- Per-install AI config.
- Clear admin setup screen or deployment documentation.
- Health check for the configured AI endpoint.

### Customer-Hosted No-Export AI

Run Shifter in the customer's environment and point `AI__BaseUrl` to a local
OpenAI-compatible inference server such as vLLM or Ollama.

Good for:
- Defense, public sector, hospitals, and organizations with strict no-data-export
  rules.
- Air-gapped or semi-air-gapped installs.

Tradeoffs:
- The customer must provide GPU/CPU capacity.
- Model quality may be lower than top hosted models.
- Vision/PDF schedule import may require a multimodal local model or must be
  disabled.

Requirements:
- No external AI calls.
- No external email provider unless explicitly configured by the customer.
- File imports and chat prompts stay inside the customer network.

## Provider Choice

The product should prefer this order:

1. OpenAI-compatible endpoint abstraction.
2. Hosted OpenAI/OpenRouter/etc. for SaaS and development.
3. Customer Azure/OpenAI-compatible gateway for enterprise cloud.
4. vLLM/Ollama local endpoint for no-export deployments.

Do not bind business logic to a provider SDK. Keep AI behind `IAiAssistant` and
provider configuration.
