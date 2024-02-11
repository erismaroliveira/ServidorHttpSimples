using System.Text;

class PaginaCadastroProduto : PaginaDinamica
{
  public override byte[] Post(SortedList<string, string> parametros)
  {
    Produto produto = new Produto();
    produto.Codigo = parametros.ContainsKey("codigo") ? 
      Convert.ToInt32(parametros["codigo"]) : produto.Codigo = 0;
    produto.Nome = parametros.ContainsKey("nome") ?
      parametros["nome"] : produto.Nome = "";

    if (produto.Codigo > 0)
      Produto.Listagem.Add(produto);
    string html = "<script>window.location.replace(\"produtos.dhtml\")</script>";
    return Encoding.UTF8.GetBytes(html);
  }
}