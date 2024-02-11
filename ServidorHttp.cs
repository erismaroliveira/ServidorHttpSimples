using System.Net;
using System.Net.Sockets;
using System.Text;

class ServidorHttp {
	private TcpListener Controlador;
	private int Porta { get; set; }
	private int QtdeRequests { get; set; }
	public string HtmlExemplo { get; set; }
	private SortedList<string, string> TiposMime { get; set; }
	private SortedList<string, string> DiretoriosHosts { get; set; }

	public ServidorHttp(int porta = 8080)
	{
		this.Porta = porta;
		this.CriarHtmlExemplo();
		this.PopularTiposMIME();
		this.PopularDiretoriosHosts();
		try
		{
			this.Controlador = new TcpListener(IPAddress.Parse("127.0.0.1"), this.Porta);
			this.Controlador.Start();
			Console.WriteLine($"Servidor HTTP rodando na porta {this.Porta}.");
			Console.WriteLine($"Para acessar, digite no navegador: http://localhost:{this.Porta}.");
			Task servidorHttpTask = Task.Run(() => AguardarRequests());
			servidorHttpTask.GetAwaiter().GetResult();
		}
		catch (Exception e)
		{
			Console.WriteLine($"Erro ao iniciar o servidor na porta {this.Porta}:\n{e.Message}");
		}
	}

	private async Task AguardarRequests()
	{
		while (true)
		{
			Socket conexao = await this.Controlador.AcceptSocketAsync();
			this.QtdeRequests++;
			Task task =  Task.Run(() => ProcessarRequest(conexao, this.QtdeRequests));
		}
	}

	private void ProcessarRequest(Socket conexao, int numeroRequest)
	{
		Console.WriteLine($"Processando Request #{numeroRequest} recebida.\n");
		if (conexao.Connected)
		{
			byte[] bytesRequisicao = new byte[1024];
			conexao.Receive(bytesRequisicao, bytesRequisicao.Length, 0);
			string textoRequisicao = Encoding.UTF8.GetString(bytesRequisicao)
				.Replace((char)0, ' ').Trim();
			if (textoRequisicao.Length > 0)
			{
				Console.WriteLine($"\n{textoRequisicao}\n");
				string[] linhas = textoRequisicao.Split("\r\n");
				int primeiroEspaco = linhas[0].IndexOf(' ');
				int segundoEspaco = linhas[0].LastIndexOf(' ');
				string metodoHttp = linhas[0].Substring(0, primeiroEspaco);
				string recursoBuscado = linhas[0].Substring(primeiroEspaco + 1, segundoEspaco - primeiroEspaco - 1);
				if (recursoBuscado == "/") recursoBuscado = "/index.html";
				string textoParametros = recursoBuscado.Contains("?") ? recursoBuscado.Split("?")[1] : "";
				recursoBuscado = recursoBuscado.Split("?")[0];
				SortedList<string, string> parametros = ProcessarParametros(textoParametros);
				string dadosPost = textoRequisicao.Contains("\r\n\r\n") ? textoRequisicao.Split("\r\n\r\n")[1] : "";
				if (!string.IsNullOrEmpty(dadosPost))
				{
					dadosPost = WebUtility.UrlDecode(dadosPost);
					var parametrosPost = ProcessarParametros(dadosPost);
					foreach (var pp in parametrosPost)
						parametros.Add(pp.Key, pp.Value);
				}
				string versaoHttp = linhas[0].Substring(segundoEspaco + 1);
				primeiroEspaco = linhas[1].IndexOf(' ');
				string nomeHost = linhas[1].Substring(primeiroEspaco + 1);

				byte[] bytesCabecalho = null;
				byte[] bytesConteudo = null;
				FileInfo infoArquivo = new FileInfo(ObterCaminhoFisicoArquivo(nomeHost, recursoBuscado));
				if (infoArquivo.Exists)
				{
					if (TiposMime.ContainsKey(infoArquivo.Extension.ToLower()))
					{
						if (infoArquivo.Extension.ToLower() == ".dhtml")
							bytesConteudo = GerarHTMLDinamico(infoArquivo.FullName, parametros, metodoHttp);
						else
							bytesConteudo = File.ReadAllBytes(infoArquivo.FullName);

						string tipoMime = TiposMime[infoArquivo.Extension.ToLower()];
						bytesCabecalho = GerarCabecalho(versaoHttp, tipoMime, "200 OK", bytesConteudo.Length);
					}
					else
					{
						bytesConteudo = Encoding.UTF8.GetBytes("<h1>Erro 415 - Tipo de Mídia Não Suportado</h1>");
						bytesCabecalho = GerarCabecalho(versaoHttp, "text/html;charset=utf-8", "415 Unsupported Media Type", bytesConteudo.Length);
					}
				}
				else
				{
					bytesConteudo = Encoding.UTF8.GetBytes("<h1>Erro 404 - Arquivo Não Encontrado</h1>");
					bytesCabecalho = GerarCabecalho(versaoHttp, "text/html;charset=utf-8", "404 Not Found", bytesConteudo.Length);
				}
				int bytesEnviados = conexao.Send(bytesCabecalho, bytesCabecalho.Length, 0);
				bytesEnviados += conexao.Send(bytesConteudo, bytesConteudo.Length, 0);
				conexao.Close();
				Console.WriteLine($"\n{bytesEnviados} bytes enviados em resposta à requisição #{numeroRequest}.");
			}
		}
		Console.WriteLine($"\nRequest #{numeroRequest} finalizado.");
	}

	private byte[] GerarCabecalho(string versaoHttp, string tipoMime, string codigoHttp, int qtdeBytes = 0)
	{
		StringBuilder texto = new StringBuilder();
		texto.Append($"{versaoHttp} {codigoHttp}{Environment.NewLine}");
		texto.Append($"Server: Servidor Http Simples 1.0{Environment.NewLine}");
		texto.Append($"Content-Type: {tipoMime}{Environment.NewLine}");
		texto.Append($"Content-Length: {qtdeBytes}{Environment.NewLine}{Environment.NewLine}");
		return Encoding.UTF8.GetBytes(texto.ToString());
	}

	private void CriarHtmlExemplo()
	{
		StringBuilder html = new StringBuilder();
		html.Append("<!DOCTYPE html>");
		html.Append("<html lang=\"pt-br\">");
		html.Append("<head>");
		html.Append("<meta charset=\"UTF-8\">");
		html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
		html.Append("<title>Servidor HTTP Simples</title>");
		html.Append("</head>");
		html.Append("<body>");
		html.Append("<h1>Servidor HTTP Simples</h1>");
		html.Append("<p>Este é um servidor HTTP simples, escrito em C#, que atende a requisições GET.</p>");
		html.Append("<p>Para acessar, digite no navegador: http://localhost:8080.</p>");
		html.Append("</body>");
		html.Append("</html>");
		this.HtmlExemplo = html.ToString();
	}

	public byte[] LerArquivo(string recurso)
	{
		string diretorio = "D:\\Projetos\\ServidorHttpSimples\\www";
		string caminhoArquivo = diretorio + recurso.Replace("/", "\\");
		if (File.Exists(caminhoArquivo))
		{
			return File.ReadAllBytes(caminhoArquivo);
		}
		else return new byte[0];
	}

	private void PopularTiposMIME()
	{
		this.TiposMime = new SortedList<string, string>();
		this.TiposMime.Add(".html", "text/html;charset=utf-8");
		this.TiposMime.Add(".htm", "text/html;charset=utf-8");
		this.TiposMime.Add(".dhtml", "text/html;charset=utf-8");
		this.TiposMime.Add(".css", "text/css;charset=utf-8");
		this.TiposMime.Add(".js", "text/javascript;charset=utf-8");
		this.TiposMime.Add(".jpg", "image/jpeg");
		this.TiposMime.Add(".jpeg", "image/jpeg");
		this.TiposMime.Add(".png", "image/png");
		this.TiposMime.Add(".gif", "image/gif");
		this.TiposMime.Add(".ico", "image/x-icon");
		this.TiposMime.Add(".xml", "text/xml;charset=utf-8");
		this.TiposMime.Add(".json", "application/json;charset=utf-8");
		this.TiposMime.Add(".pdf", "application/pdf");
		this.TiposMime.Add(".zip", "application/zip");
		this.TiposMime.Add(".rar", "application/x-rar-compressed");
		this.TiposMime.Add(".mp3", "audio/mpeg");
		this.TiposMime.Add(".mp4", "video/mp4");
		this.TiposMime.Add(".avi", "video/x-msvideo");
		this.TiposMime.Add(".mpeg", "video/mpeg");
		this.TiposMime.Add(".webm", "video/webm");
		this.TiposMime.Add(".ogg", "video/ogg");
		this.TiposMime.Add(".flv", "video/x-flv");
		this.TiposMime.Add(".wmv", "video/x-ms-wmv");
		this.TiposMime.Add(".mov", "video/quicktime");
		this.TiposMime.Add(".3gp", "video/3gpp");
		this.TiposMime.Add(".3g2", "video/3gpp2");
		this.TiposMime.Add(".svg", "image/svg+xml");
		this.TiposMime.Add(".ttf", "font/ttf");
		this.TiposMime.Add(".otf", "font/otf");
		this.TiposMime.Add(".woff", "font/woff");
		this.TiposMime.Add(".woff2", "font/woff2");
		this.TiposMime.Add(".eot", "application/vnd.ms-fontobject");
		this.TiposMime.Add(".appcache", "text/cache-manifest");
		this.TiposMime.Add(".manifest", "text/cache-manifest");
		this.TiposMime.Add(".webmanifest", "application/manifest+json");
		this.TiposMime.Add(".lolohtml", "text/html;charset=utf-8");
	}

	private void PopularDiretoriosHosts()
	{
		this.DiretoriosHosts = new SortedList<string, string>();
		this.DiretoriosHosts.Add("localhost", "D:\\Projetos\\ServidorHttpSimples\\www\\localhost");
		this.DiretoriosHosts.Add("erismardev.com", "D:\\Projetos\\ServidorHttpSimples\\www\\erismardev.com");
	}

	public string ObterCaminhoFisicoArquivo(string host, string arquivo)
	{
		string diretorio = this.DiretoriosHosts[host.Split(":")[0]];
		string caminhoArquivo = diretorio + arquivo.Replace("/", "\\");
		return caminhoArquivo;
	}

	public byte[] GerarHTMLDinamico(string caminhoArquivo, SortedList<string, string> parametros, string metodoHttp)
	{
		FileInfo infoArquivo = new FileInfo(caminhoArquivo);
		string nomeClassePagina = "Pagina" + infoArquivo.Name.Replace(infoArquivo.Extension, "");
		Type tipoPaginaDinamica = Type.GetType(nomeClassePagina, true, true);
		PaginaDinamica pd = Activator.CreateInstance(tipoPaginaDinamica) as PaginaDinamica;
		pd.HtmlModelo = File.ReadAllText(caminhoArquivo);
		switch (metodoHttp.ToLower())
		{
			case "get":
				return pd.Get(parametros);
			case "post":
				return pd.Post(parametros);
			default:
				return new byte[0];
		}
		// string coringa = "{{HtmlGerado}}";
		// string htmlModelo = File.ReadAllText(caminhoArquivo);
		// StringBuilder htmlGerado = new StringBuilder();
		// htmlGerado.Append("<ul>");
		// foreach (var tipo in this.TiposMime.Keys)
		// {
		// 	htmlGerado.Append($"<li>Arquivos com extensão {tipo}</li>");
		// }
		// htmlGerado.Append("</ul>");
		// if (parametros.Count > 0)
		// {
		// 	htmlGerado.Append("<ul>");
		// 	foreach (var p in parametros)
		// 	{
		// 		htmlGerado.Append($"<li>{p.Key}={p.Value}</li>");
		// 	}
		// 	htmlGerado.Append("</ul>");
		// }
		// else
		// {
		// 	htmlGerado.Append("<p>Nenhum parâmetro foi passado na URL.</p>");
		// }
		// string textoHtmlGerado = htmlModelo.Replace(coringa, htmlGerado.ToString());
		// return Encoding.UTF8.GetBytes(textoHtmlGerado, 0, textoHtmlGerado.Length);
	}

	private SortedList<string, string> ProcessarParametros(string textoParametros)
	{
		SortedList<string, string> parametros = new SortedList<string, string>();
		
		if (!string.IsNullOrEmpty(textoParametros.Trim()))
		{
			string[] paresChaveValor = textoParametros.Split("&");
			foreach (var par in paresChaveValor)
			{
				parametros.Add(par.Split("=")[0].ToLower(), par.Split("=")[1]);
			}
		}
		return parametros;
	}
}