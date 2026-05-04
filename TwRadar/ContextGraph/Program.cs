// ============================================================
//  Program08 — Context Graph (Grafo de Contexto)
// ============================================================
// Este exemplo demonstra o conceito de Context Graph:
// 
// Um Context Graph é uma técnica de representação de conhecimento
// onde decisões, políticas, exceções, precedentes e evidências
// são modelados como nós conectados em um grafo temporal.
//
// Diferentemente de sistemas de registro (que capturam O QUE aconteceu),
// um Context Graph captura O PORQUÊ, transformando raciocínio
// institucional em estrutura consultável e legível por máquina.
//
// Características principais:
// 1. Nós de primeira classe para decisões, políticas, precedentes
// 2. Validade temporal em cada aresta (fatos superados são invalidados)
// 3. Rastreamento de procedência e cadeias causais
// 4. Memória persistente entre sessões
//
// Neste exemplo, criamos um sistema de aprovação de descontos
// onde um agente de IA pode:
// - Consultar políticas vigentes vs. expiradas
// - Identificar precedentes relevantes
// - Raciocinar através de cadeias de decisão multi-hop
// - Distinguir entre política permanente e exceção pontual
//
// Uso:
//   dotnet run -- 08 "sua pergunta sobre descontos"
//
// Exemplos:
//   dotnet run -- 08 "Posso dar 15% de desconto para um cliente VIP?"
//   dotnet run -- 08 "Por que aprovamos 20% de desconto na ordem #1234?"
//   dotnet run -- 08 "Qual é a política atual de descontos para B2B?"
// ============================================================

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.Text.Json;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// ── Main Program ──────────────────────────────────────────
var endpoint = "https://models.github.ai/inference";
var credential = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
var model = "openai/gpt-4o-mini";

if (string.IsNullOrWhiteSpace(credential))
{
	Console.Error.WriteLine("Erro: variável de ambiente GITHUB_TOKEN não configurada.");
	Environment.Exit(1);
}

// Construir o kernel com GitHub Models
var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri(endpoint);

var kernel = Kernel.CreateBuilder()
	.AddOpenAIChatCompletion(
		modelId: model,
		apiKey: credential,
		httpClient: httpClient)
	.Build();

// ── Context Graph Plugin ────────────────────────────────────
// Inicializar o grafo de contexto com dados históricos
var contextGraph = new ContextGraph();
SeedContextGraph(contextGraph);

// Registrar o plugin que permite consultar o grafo
kernel.ImportPluginFromObject(new ContextGraphPlugin(contextGraph), "ContextGraph");

// Obter pergunta do usuário (ou usar padrão)
string userQuery = args.Length > 1
	? string.Join(" ", args.Skip(1))
	: "Posso dar 15% de desconto para um cliente VIP?";

Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
Console.WriteLine("║         Context Graph - Raciocínio com Histórico         ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine($"🤖 Pergunta: {userQuery}");
Console.WriteLine();

// ── Configurar Auto Function Calling ───────────────────────
var executionSettings = new OpenAIPromptExecutionSettings
{
	ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
	Temperature = 0.7,
	MaxTokens = 1500
};

Console.WriteLine("🕸️  Context Graph: Sistema inicializado");
Console.WriteLine($"   - {contextGraph.GetAllNodes().Count()} nós no grafo");
Console.WriteLine($"   - {contextGraph.GetAllEdges().Count()} arestas temporais");
Console.WriteLine();
Console.WriteLine("🔧 Ferramentas disponíveis:");
Console.WriteLine("   - QueryActivePolicies: Consulta políticas vigentes");
Console.WriteLine("   - QueryPrecedents: Busca precedentes relevantes");
Console.WriteLine("   - TraceDecisionChain: Rastreia cadeia de decisão");
Console.WriteLine("   - CheckPolicyValidity: Verifica validade temporal");
Console.WriteLine();

// ── Executar com Auto Function Calling ─────────────────────
Console.WriteLine("💡 Processando solicitação com Context Graph...");
Console.WriteLine();

var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage(
	"Você é um assistente de aprovação de descontos que tem acesso a um Context Graph " +
	"com histórico completo de decisões, políticas e precedentes da empresa. " +
	"\n\nSempre que analisar uma solicitação de desconto:" +
	"\n1. Consulte as políticas VIGENTES (não use políticas expiradas)" +
	"\n2. Busque precedentes relevantes para o caso" +
	"\n3. Rastreie as cadeias de decisão quando necessário" +
	"\n4. Explique CLARAMENTE o raciocínio, citando:" +
	"\n   - Qual política se aplica e desde quando está vigente" +
	"\n   - Se há precedentes e se foram exceções ou política permanente" +
	"\n   - A cadeia causal que justifica a decisão" +
	"\n\nSeja preciso e transparente sobre a procedência de cada recomendação."
);
chatHistory.AddUserMessage(userQuery);

var chat = kernel.GetRequiredService<IChatCompletionService>();
var response = await chat.GetChatMessageContentAsync(
	chatHistory,
	executionSettings: executionSettings,
	kernel: kernel
);

Console.WriteLine("📝 Resposta do Assistente:");
Console.WriteLine("─────────────────────────────────────────────────────────");
Console.WriteLine(response.Content);
Console.WriteLine("─────────────────────────────────────────────────────────");
Console.WriteLine();

// ── Demonstrar evolução temporal ────────────────────────────
Console.WriteLine("🔄 Demonstrando consulta com evolução temporal...");
Console.WriteLine();

chatHistory.Add(response);
chatHistory.AddUserMessage(
	"Mostre-me como a política de descontos para B2B mudou ao longo do tempo"
);

response = await chat.GetChatMessageContentAsync(
	chatHistory,
	executionSettings: executionSettings,
	kernel: kernel
);

Console.WriteLine("📝 Resposta do Assistente:");
Console.WriteLine("─────────────────────────────────────────────────────────");
Console.WriteLine(response.Content);
Console.WriteLine("─────────────────────────────────────────────────────────");
Console.WriteLine();
Console.WriteLine("✨ Demo Context Graph concluída com sucesso!");

// ── Seed Context Graph ──────────────────────────────────────
static void SeedContextGraph(ContextGraph graph)
{
	var now = DateTime.UtcNow;

	// ═══════════════════════════════════════════════════════════
	//  POLÍTICAS DE DESCONTO
	// ═══════════════════════════════════════════════════════════

	// Política antiga de B2B (expirada)
	var oldB2BPolicy = graph.AddNode(new ContextNode
	{
		Id = "policy-b2b-2023",
		Type = NodeType.Policy,
		Title = "Política B2B 2023",
		Content = "Desconto máximo de 10% para clientes B2B sem aprovação gerencial",
		Metadata = new Dictionary<string, object>
		{
			["category"] = "discount",
			["segment"] = "B2B",
			["maxDiscount"] = 10
		}
	});

	// Política atual de B2B (vigente)
	var currentB2BPolicy = graph.AddNode(new ContextNode
	{
		Id = "policy-b2b-2024",
		Type = NodeType.Policy,
		Title = "Política B2B 2024 (Atualizada)",
		Content = "Desconto máximo de 15% para clientes B2B sem aprovação. Até 25% com aprovação de gerente regional.",
		Metadata = new Dictionary<string, object>
		{
			["category"] = "discount",
			["segment"] = "B2B",
			["maxDiscount"] = 15,
			["maxWithApproval"] = 25
		}
	});

	// Política VIP (vigente)
	var vipPolicy = graph.AddNode(new ContextNode
	{
		Id = "policy-vip-current",
		Type = NodeType.Policy,
		Title = "Política VIP",
		Content = "Clientes VIP podem receber até 20% de desconto sem aprovação adicional",
		Metadata = new Dictionary<string, object>
		{
			["category"] = "discount",
			["segment"] = "VIP",
			["maxDiscount"] = 20
		}
	});

	// Conectar evolução de políticas B2B
	graph.AddEdge(new ContextEdge
	{
		Id = "edge-b2b-supersedes",
		SourceId = currentB2BPolicy.Id,
		TargetId = oldB2BPolicy.Id,
		Type = EdgeType.Supersedes,
		ValidFrom = now.AddMonths(-2), // Nova política há 2 meses
		ValidUntil = null,
		Metadata = new Dictionary<string, object>
		{
			["reason"] = "Expansão de competitividade no mercado B2B"
		}
	});

	// Marcar política antiga como expirada
	graph.AddEdge(new ContextEdge
	{
		Id = "edge-b2b-old-expired",
		SourceId = oldB2BPolicy.Id,
		TargetId = currentB2BPolicy.Id,
		Type = EdgeType.ExpiredBy,
		ValidFrom = now.AddYears(-1),
		ValidUntil = now.AddMonths(-2), // Válida até 2 meses atrás
		Metadata = new Dictionary<string, object>()
	});

	// ═══════════════════════════════════════════════════════════
	//  DECISÕES E PRECEDENTES
	// ═══════════════════════════════════════════════════════════

	// Decisão: Aprovação excepcional de 20% para B2B
	var exceptionDecision = graph.AddNode(new ContextNode
	{
		Id = "decision-order-1234",
		Type = NodeType.Decision,
		Title = "Aprovação Ordem #1234 - Desconto 20%",
		Content = "Aprovado desconto de 20% para TechCorp devido a contrato de volume anual de $500k",
		Metadata = new Dictionary<string, object>
		{
			["orderId"] = "1234",
			["customer"] = "TechCorp",
			["discount"] = 20,
			["approver"] = "Maria Silva (Diretora Regional)",
			["date"] = now.AddDays(-30).ToString("yyyy-MM-dd")
		}
	});

	// Evidência da decisão
	var evidence1234 = graph.AddNode(new ContextNode
	{
		Id = "evidence-contract-techcorp",
		Type = NodeType.Evidence,
		Title = "Contrato Anual TechCorp",
		Content = "Contrato de volume anual de $500k assinado em janeiro/2024",
		Metadata = new Dictionary<string, object>
		{
			["contractValue"] = 500000,
			["term"] = "12 months"
		}
	});

	// Conectar decisão à evidência
	graph.AddEdge(new ContextEdge
	{
		Id = "edge-decision-evidence",
		SourceId = exceptionDecision.Id,
		TargetId = evidence1234.Id,
		Type = EdgeType.BasedOn,
		ValidFrom = now.AddDays(-30),
		ValidUntil = null,
		Metadata = new Dictionary<string, object>()
	});

	// Conectar decisão à política que foi excedida
	graph.AddEdge(new ContextEdge
	{
		Id = "edge-decision-policy",
		SourceId = exceptionDecision.Id,
		TargetId = currentB2BPolicy.Id,
		Type = EdgeType.ExceptionTo,
		ValidFrom = now.AddDays(-30),
		ValidUntil = null,
		Metadata = new Dictionary<string, object>
		{
			["reason"] = "Volume anual justifica exceção à política padrão"
		}
	});

	// ─────────────────────────────────────────────────────────

	// Decisão: Negação de desconto excessivo
	var denialDecision = graph.AddNode(new ContextNode
	{
		Id = "decision-order-5678",
		Type = NodeType.Decision,
		Title = "Negação Ordem #5678 - Desconto 30%",
		Content = "Negado desconto de 30% para SmallBiz. Excede limites mesmo com aprovação gerencial.",
		Metadata = new Dictionary<string, object>
		{
			["orderId"] = "5678",
			["customer"] = "SmallBiz",
			["requestedDiscount"] = 30,
			["denier"] = "João Santos (Gerente Regional)",
			["date"] = now.AddDays(-15).ToString("yyyy-MM-dd")
		}
	});

	// Conectar negação à política
	graph.AddEdge(new ContextEdge
	{
		Id = "edge-denial-policy",
		SourceId = denialDecision.Id,
		TargetId = currentB2BPolicy.Id,
		Type = EdgeType.EnforcedBy,
		ValidFrom = now.AddDays(-15),
		ValidUntil = null,
		Metadata = new Dictionary<string, object>
		{
			["reason"] = "Desconto solicitado excede limite máximo com aprovação (25%)"
		}
	});

	// ─────────────────────────────────────────────────────────

	// Precedente: Aprovações VIP consistentes
	var vipPrecedent = graph.AddNode(new ContextNode
	{
		Id = "precedent-vip-15-20",
		Type = NodeType.Precedent,
		Title = "Precedente: Descontos VIP 15-20%",
		Content = "Histórico de 15 aprovações de descontos entre 15-20% para clientes VIP nos últimos 6 meses",
		Metadata = new Dictionary<string, object>
		{
			["count"] = 15,
			["avgDiscount"] = 17.5,
			["period"] = "6 months"
		}
	});

	// Conectar precedente à política VIP
	graph.AddEdge(new ContextEdge
	{
		Id = "edge-precedent-policy",
		SourceId = vipPrecedent.Id,
		TargetId = vipPolicy.Id,
		Type = EdgeType.SupportsPolicy,
		ValidFrom = now.AddMonths(-6),
		ValidUntil = null,
		Metadata = new Dictionary<string, object>()
	});

	// ═══════════════════════════════════════════════════════════
	//  CADEIA CAUSAL: Por que mudamos a política B2B?
	// ═══════════════════════════════════════════════════════════

	// Evidência de mercado
	var marketEvidence = graph.AddNode(new ContextNode
	{
		Id = "evidence-market-analysis",
		Type = NodeType.Evidence,
		Title = "Análise de Mercado Q4/2023",
		Content = "Concorrentes oferecem 15-20% de desconto padrão para B2B. Perdemos 5 grandes contas.",
		Metadata = new Dictionary<string, object>
		{
			["lostAccounts"] = 5,
			["competitorDiscount"] = "15-20%"
		}
	});

	// Decisão de mudança de política
	var policyChangeDecision = graph.AddNode(new ContextNode
	{
		Id = "decision-policy-change-2024",
		Type = NodeType.Decision,
		Title = "Decisão: Atualizar Política B2B",
		Content = "Aprovada atualização da política B2B para manter competitividade",
		Metadata = new Dictionary<string, object>
		{
			["approver"] = "Conselho Executivo",
			["date"] = now.AddMonths(-2).ToString("yyyy-MM-dd")
		}
	});

	// Conectar evidência -> decisão -> nova política
	graph.AddEdge(new ContextEdge
	{
		Id = "edge-evidence-to-decision",
		SourceId = policyChangeDecision.Id,
		TargetId = marketEvidence.Id,
		Type = EdgeType.BasedOn,
		ValidFrom = now.AddMonths(-2),
		ValidUntil = null,
		Metadata = new Dictionary<string, object>()
	});

	graph.AddEdge(new ContextEdge
	{
		Id = "edge-decision-to-newpolicy",
		SourceId = currentB2BPolicy.Id,
		TargetId = policyChangeDecision.Id,
		Type = EdgeType.ResultOf,
		ValidFrom = now.AddMonths(-2),
		ValidUntil = null,
		Metadata = new Dictionary<string, object>()
	});
}

// ══════════════════════════════════════════════════════════
//  Context Graph - Classes de Domínio
// ══════════════════════════════════════════════════════════

enum NodeType
{
	Policy,      // Política ou regra de negócio
	Decision,    // Decisão tomada
	Precedent,   // Precedente estabelecido
	Evidence,    // Evidência ou justificativa
	Exception    // Exceção documentada
}

enum EdgeType
{
	BasedOn,        // Decisão baseada em evidência
	Supersedes,     // Nova versão substitui antiga
	ExceptionTo,    // Exceção a uma política
	EnforcedBy,     // Aplicação de política
	ExpiredBy,      // Expirada por nova versão
	SupportsPolicy, // Precedente que suporta política
	ResultOf        // Resultado de uma decisão
}

class ContextNode
{
	public string Id { get; set; } = string.Empty;
	public NodeType Type { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Content { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public Dictionary<string, object> Metadata { get; set; } = new();
}

class ContextEdge
{
	public string Id { get; set; } = string.Empty;
	public string SourceId { get; set; } = string.Empty;
	public string TargetId { get; set; } = string.Empty;
	public EdgeType Type { get; set; }
	public DateTime ValidFrom { get; set; }
	public DateTime? ValidUntil { get; set; } // null = ainda válido
	public Dictionary<string, object> Metadata { get; set; } = new();
}

class ContextGraph
{
	private readonly Dictionary<string, ContextNode> _nodes = new();
	private readonly List<ContextEdge> _edges = new();

	public ContextNode AddNode(ContextNode node)
	{
		_nodes[node.Id] = node;
		return node;
	}

	public ContextEdge AddEdge(ContextEdge edge)
	{
		_edges.Add(edge);
		return edge;
	}

	public ContextNode? GetNode(string id) => _nodes.GetValueOrDefault(id);

	public IEnumerable<ContextNode> GetAllNodes() => _nodes.Values;

	public IEnumerable<ContextEdge> GetAllEdges() => _edges;

	public IEnumerable<ContextEdge> GetEdgesFrom(string nodeId, DateTime? asOf = null)
	{
		var timestamp = asOf ?? DateTime.UtcNow;
		return _edges.Where(e =>
			e.SourceId == nodeId &&
			e.ValidFrom <= timestamp &&
			(e.ValidUntil == null || e.ValidUntil > timestamp)
		);
	}

	public IEnumerable<ContextEdge> GetEdgesTo(string nodeId, DateTime? asOf = null)
	{
		var timestamp = asOf ?? DateTime.UtcNow;
		return _edges.Where(e =>
			e.TargetId == nodeId &&
			e.ValidFrom <= timestamp &&
			(e.ValidUntil == null || e.ValidUntil > timestamp)
		);
	}

	public IEnumerable<ContextNode> GetActiveNodes(NodeType type, DateTime? asOf = null)
	{
		var timestamp = asOf ?? DateTime.UtcNow;
		var expiredNodeIds = _edges
			.Where(e => e.Type == EdgeType.ExpiredBy && e.ValidUntil <= timestamp)
			.Select(e => e.SourceId)
			.ToHashSet();

		return _nodes.Values
			.Where(n => n.Type == type && !expiredNodeIds.Contains(n.Id));
	}

	public IEnumerable<ContextNode> TraceChain(string startNodeId, EdgeType edgeType, int maxDepth = 5)
	{
		var visited = new HashSet<string>();
		var result = new List<ContextNode>();
		var queue = new Queue<(string nodeId, int depth)>();
		queue.Enqueue((startNodeId, 0));

		while (queue.Count > 0)
		{
			var (currentId, depth) = queue.Dequeue();
			if (depth >= maxDepth || visited.Contains(currentId))
				continue;

			visited.Add(currentId);
			var node = GetNode(currentId);
			if (node != null)
				result.Add(node);

			var edges = GetEdgesFrom(currentId).Where(e => e.Type == edgeType);
			foreach (var edge in edges)
				queue.Enqueue((edge.TargetId, depth + 1));
		}

		return result;
	}
}

// ══════════════════════════════════════════════════════════
//  Context Graph Plugin - Ferramentas para IA
// ══════════════════════════════════════════════════════════

class ContextGraphPlugin
{
	private readonly ContextGraph _graph;

	public ContextGraphPlugin(ContextGraph graph)
	{
		_graph = graph;
	}

	/// <summary>
	/// Consulta políticas ativas para uma categoria específica
	/// </summary>
	[KernelFunction, Description("Consulta políticas VIGENTES (não expiradas) para uma categoria específica como 'discount', 'approval', etc.")]
	public string QueryActivePolicies(
		[Description("Categoria da política (ex: 'discount'). Use 'all' para todas as categorias")]
		string category = "all")
	{
		Console.WriteLine($"   🔧 Context Graph: QueryActivePolicies(category: '{category}')");

		var activePolicies = _graph.GetActiveNodes(NodeType.Policy);

		if (category != "all")
		{
			activePolicies = activePolicies.Where(p =>
				p.Metadata.TryGetValue("category", out var cat) &&
				cat.ToString()!.Equals(category, StringComparison.OrdinalIgnoreCase)
			);
		}

		var policies = activePolicies.Select(p => new
		{
			id = p.Id,
			title = p.Title,
			content = p.Content,
			createdAt = p.CreatedAt,
			metadata = p.Metadata,
			supersededPolicies = _graph.GetEdgesFrom(p.Id)
				.Where(e => e.Type == EdgeType.Supersedes)
				.Select(e => _graph.GetNode(e.TargetId)?.Title)
				.ToList()
		}).ToList();

		return JsonSerializer.Serialize(new
		{
			category = category,
			count = policies.Count,
			timestamp = DateTime.UtcNow,
			policies = policies
		}, new JsonSerializerOptions { WriteIndented = true });
	}

	/// <summary>
	/// Busca precedentes relevantes baseado em critérios
	/// </summary>
	[KernelFunction, Description("Busca precedentes históricos relevantes baseado em segmento de cliente, tipo de desconto ou outros critérios")]
	public string QueryPrecedents(
		[Description("Termo de busca para filtrar precedentes (ex: 'VIP', 'B2B', 'volume')")]
		string searchTerm)
	{
		Console.WriteLine($"   🔧 Context Graph: QueryPrecedents(searchTerm: '{searchTerm}')");

		var precedents = _graph.GetAllNodes()
			.Where(n => n.Type == NodeType.Precedent)
			.Where(n =>
				n.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
				n.Content.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
				n.Metadata.Values.Any(v => v.ToString()!.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
			)
			.Select(p => new
			{
				id = p.Id,
				title = p.Title,
				content = p.Content,
				metadata = p.Metadata,
				supportedPolicies = _graph.GetEdgesFrom(p.Id)
					.Where(e => e.Type == EdgeType.SupportsPolicy)
					.Select(e => _graph.GetNode(e.TargetId)?.Title)
					.ToList()
			})
			.ToList();

		return JsonSerializer.Serialize(new
		{
			searchTerm = searchTerm,
			count = precedents.Count,
			precedents = precedents
		}, new JsonSerializerOptions { WriteIndented = true });
	}

	/// <summary>
	/// Rastreia cadeia de decisão completa para entender o "porquê"
	/// </summary>
	[KernelFunction, Description("Rastreia a cadeia causal completa de uma decisão, mostrando evidências, políticas aplicadas e exceções")]
	public string TraceDecisionChain(
		[Description("ID da decisão ou ordem para rastrear (ex: 'decision-order-1234', 'policy-b2b-2024')")]
		string nodeId)
	{
		Console.WriteLine($"   🔧 Context Graph: TraceDecisionChain(nodeId: '{nodeId}')");

		var node = _graph.GetNode(nodeId);
		if (node == null)
		{
			return JsonSerializer.Serialize(new
			{
				error = $"Nó '{nodeId}' não encontrado",
				availableNodes = _graph.GetAllNodes().Select(n => n.Id).ToList()
			});
		}

		// Rastrear evidências (BasedOn)
		var evidences = _graph.GetEdgesFrom(nodeId)
			.Where(e => e.Type == EdgeType.BasedOn)
			.Select(e => _graph.GetNode(e.TargetId))
			.Where(n => n != null)
			.Select(n => new { n!.Id, n.Title, n.Content, n.Metadata })
			.ToList();

		// Rastrear políticas relacionadas
		var policies = _graph.GetEdgesFrom(nodeId)
			.Where(e => e.Type == EdgeType.ExceptionTo || e.Type == EdgeType.EnforcedBy)
			.Select(e => new
			{
				policyNode = _graph.GetNode(e.TargetId),
				relationship = e.Type.ToString(),
				reason = e.Metadata.GetValueOrDefault("reason", "")
			})
			.Where(x => x.policyNode != null)
			.Select(x => new
			{
				x.policyNode!.Id,
				x.policyNode.Title,
				x.policyNode.Content,
				x.relationship,
				x.reason
			})
			.ToList();

		// Rastrear cadeia de resultados (ResultOf)
		var resultChain = _graph.TraceChain(nodeId, EdgeType.ResultOf, maxDepth: 3)
			.Select(n => new { n.Id, n.Title, n.Type })
			.ToList();

		return JsonSerializer.Serialize(new
		{
			node = new
			{
				node.Id,
				node.Type,
				node.Title,
				node.Content,
				node.Metadata
			},
			evidences = evidences,
			policies = policies,
			resultChain = resultChain,
			timestamp = DateTime.UtcNow
		}, new JsonSerializerOptions { WriteIndented = true });
	}

	/// <summary>
	/// Verifica validade temporal de uma política em um ponto no tempo
	/// </summary>
	[KernelFunction, Description("Verifica se uma política estava válida em uma data específica e mostra seu histórico de evolução")]
	public string CheckPolicyValidity(
		[Description("Segmento da política para verificar (ex: 'B2B', 'VIP')")]
		string segment,
		[Description("Data para verificar no formato YYYY-MM-DD. Use 'now' para data atual")]
		string dateString = "now")
	{
		Console.WriteLine($"   🔧 Context Graph: CheckPolicyValidity(segment: '{segment}', date: '{dateString}')");

		DateTime checkDate = dateString == "now" ? DateTime.UtcNow : DateTime.Parse(dateString);

		// Encontrar políticas do segmento
		var segmentPolicies = _graph.GetAllNodes()
			.Where(n => n.Type == NodeType.Policy)
			.Where(n => n.Metadata.TryGetValue("segment", out var seg) &&
					   seg.ToString()!.Equals(segment, StringComparison.OrdinalIgnoreCase))
			.ToList();

		// Para cada política, verificar validade temporal
		var policyHistory = segmentPolicies.Select(p =>
		{
			var expirationEdges = _graph.GetEdgesFrom(p.Id)
				.Where(e => e.Type == EdgeType.ExpiredBy)
				.ToList();

			var supersededByEdges = _graph.GetEdgesTo(p.Id)
				.Where(e => e.Type == EdgeType.Supersedes)
				.ToList();

			bool isValidAtCheckDate = !expirationEdges.Any(e => e.ValidUntil <= checkDate);

			return new
			{
				p.Id,
				p.Title,
				p.Content,
				p.CreatedAt,
				p.Metadata,
				isValidAtCheckDate = isValidAtCheckDate,
				expirationDate = expirationEdges.FirstOrDefault()?.ValidUntil,
				supersededBy = supersededByEdges.Select(e => _graph.GetNode(e.SourceId)?.Title).ToList()
			};
		}).OrderBy(p => p.CreatedAt).ToList();

		return JsonSerializer.Serialize(new
		{
			segment = segment,
			checkDate = checkDate,
			currentlyValid = policyHistory.Where(p => p.isValidAtCheckDate).Select(p => p.Title).ToList(),
			policyHistory = policyHistory
		}, new JsonSerializerOptions { WriteIndented = true });
	}
}
