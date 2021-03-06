﻿using System.Web.OData.Builder;
using Microsoft.OData.Edm;

namespace WebStack.QA.Test.OData.LowerCamelCase
{
    public class LowerCamelCaseEdmModel
    {
        public static IEdmModel GetConventionModel()
        {
            ODataConventionModelBuilder builder = new ODataConventionModelBuilder();
            EntitySetConfiguration<Employee> employees = builder.EntitySet<Employee>("Employees");
            EntityTypeConfiguration<Employee> employee = employees.EntityType;
            employee.EnumProperty<Gender>(e => e.Sex).Name = "Gender";

            employee.Collection.Function("GetEarliestTwoEmployees").ReturnsCollectionFromEntitySet<Employee>("Employees");

            var functionConfiguration = builder.Function("GetAddress");
            functionConfiguration.Parameter<int>("id");
            functionConfiguration.Returns<Address>();

            var actionConfiguration = builder.Action("SetAddress");
            actionConfiguration.Parameter<int>("id");
            actionConfiguration.Parameter<Address>("address");
            actionConfiguration.ReturnsFromEntitySet(employees);

            var resetDataSource = builder.Action("ResetDataSource");

            builder.Namespace = typeof(Employee).Namespace;
            builder.EnableLowerCamelCase();
            var edmModel = builder.GetEdmModel();
            return edmModel;
        }
    }
}
