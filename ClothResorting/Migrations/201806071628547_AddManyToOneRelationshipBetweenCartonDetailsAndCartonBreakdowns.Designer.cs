// <auto-generated />
namespace ClothResorting.Migrations
{
    using System.CodeDom.Compiler;
    using System.Data.Entity.Migrations;
    using System.Data.Entity.Migrations.Infrastructure;
    using System.Resources;
    
    [GeneratedCode("EntityFramework.Migrations", "6.1.3-40302")]
    public sealed partial class AddManyToOneRelationshipBetweenCartonDetailsAndCartonBreakdowns : IMigrationMetadata
    {
        private readonly ResourceManager Resources = new ResourceManager(typeof(AddManyToOneRelationshipBetweenCartonDetailsAndCartonBreakdowns));
        
        string IMigrationMetadata.Id
        {
            get { return "201806071628547_AddManyToOneRelationshipBetweenCartonDetailsAndCartonBreakdowns"; }
        }
        
        string IMigrationMetadata.Source
        {
            get { return null; }
        }
        
        string IMigrationMetadata.Target
        {
            get { return Resources.GetString("Target"); }
        }
    }
}