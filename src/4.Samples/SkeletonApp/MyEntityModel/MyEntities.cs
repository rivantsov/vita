using System;
using Vita.Entities;

namespace MyEntityModel 
{
  [Entity]
  public interface ICustomer
  {
    [PrimaryKey, Auto]
    Guid Id { get; }

    [Size(50)]
    string Name { get; set; }

    [Size(50)]
    string Email { get; set; }
  }
}
