namespace Cliente.Models
{
    // Klase honek Pokemon baten mugimendu bat definitzen du.
    public class Move
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? Tipo { get; set; }
        public string? Categoria { get; set; }
        public int? Poder { get; set; }
        public int? Precision { get; set; }
        public int? PP { get; set; }
    }
}