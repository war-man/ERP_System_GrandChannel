namespace ClothResorting.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddedPreLocationInRegularCartonDetail : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.RegularCartonDetails", "PreLocation", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.RegularCartonDetails", "PreLocation");
        }
    }
}
