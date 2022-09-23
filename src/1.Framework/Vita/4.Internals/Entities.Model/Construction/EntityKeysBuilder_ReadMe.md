## About EntityKeysBuilder and what it does 
This class was added around Sept 2022 to handle some complex scenarios of Keys processing - I separated it from EntityModelBuilder class which used to handle keys but much simpler cases only. The complex scenario is when keys (primary and foreign) are composite, and depend on each other. The prior implementation did not handle it correctly and app start simple failed. This case came up in one real-world db-first scenario - there was already a database (note - not badly designed) with keys, primary and foreign, intermingled in a complex way. So VITA's dbfirst tool generated entities and other stuff, but then the app failed to load - failed at key processing phase. I failed to figure out a better way to specify the keys at code-gen time; the issue was deeper, the VITA just could not handle it. 

The following short example illustrates the trouble. 

```csharp
[Entity]
public interface ICustomer {
    [PrimaryKey]
    int Id {get; set;}

    string Name {get; set;}
}

[Entity]
[PrimaryKey("Customer,Id")]
public interface IInvoice {
    ICustomer Customer {get; set;}
    int Id {get; set; }

    decimal Total {get; set;}  
    //etc
}

[Entity]
[PrimaryKey("CustId,Id")] //CustId is coming from under Invoice member
public interface IInvoiceLine {
    [EntityRef(KeyColumns="CustId,InvId")] //references Customer_Id, Id columns in IInvoice table
    IInvoice Invoice {get; set;}
    
    int Id {get; set; }

    decimal Amount {get; set;}  
    //etc
}
```

We have Customers, Invoices, and Invoice lines. The central object is Customer, and the customer_id column is in all other tables and is part of tables' composite primary key (PK). PK for these other tables is a pair: (Customer_Id, id), where Id is an extra int column, an Id within customer. Customer_Id servers as partitioning and clustering key in all tables, supposedly for query efficiency. The Customer_Id column is essentially partitioning and clustering key for all tables - for read efficiency. 

 This is the case for IInvoice entity, composite PK (Customer_Id,Id). The Customer_Id comes from the FK in Customer member. For IInvoiceLine it is more complex. If we try to do similar PK (Customer_Id, Id), we need Customer_Id, so seems like we need to add a Customer member. But we have already Customer_Id column - it is in FK columns for Invoice reference (IInvoice's PK is (CustomerId, Id)). But, the FK column names for IInvoiceLine.Invoice are renamed to (CustId,InvId) - see EntityRef attribute on Invoice property. As a result, we have to specify PK as [PrimaryKey("CustId,Id")] - referencing column that's not exposed as property on entity. This represents a challenge for key processing at startup. 
 
 The old key processing algorithm fails at this point - the member list of a key should reference only existing members. The new EntityKeysBuilder is created to handle this better - it proceeds with keys/columns in a certain order. First create PK for Invoice, then FK columns in IInvoiceLine, and only then the PK for IInvoiceLine. 

 EntityKeysBuilder handles this case by first handling all simple PKs (single non-ref column); then simple FKs (refs to single PK); then composite PKs and FKs, carefully figuring out the correct order, based on dependencies. 

 

