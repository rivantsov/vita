using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

using Vita.Entities;
using Vita.Entities.Model;
using Vita.Entities.Authorization;
using Vita.Modules.Login;
using Vita.Entities.Linq;
using Vita.Common;

namespace Vita.Samples.BookStore {

  /*  ROLES:
   * Anonymous  - can browse books, publishers, authors, reviews, 
   * Customer   - logged in user; same as Anonymous plus create/edit reviews, buy books (create book orders), optionall using coupon codes, 
   *              view/edit his orders
   * Author -     can browse all books, reviews; can update his own Bio; cannot edit books, except modifying 
   *              Abstract  and Descriptions of his books. Author is allowed to update Bio, book Abstract and Description
   *              ONLY within explicitly started activity AuthorEdit.
   * BookEditor - can create books, authors, but not publishers; cannot see any book orders or coupons
   * CustomerSupport - can view user information for customers and authors, can see customer orders
   * StoreManager - can create publishers, coupons, can adjust orders; can moderate (delete any) reviews 
   */

  public class BooksAuthorization {
    // A key for OperationContext.Values dictionary to store the ID of order under adjustment
    public const string AdjustedOrderIdKey = "AdjustedOrderId";

    public Role AnonymousUser;
    public Role Customer;
    public Role Author;
    public Role BookEditor;
    public Role CustomerSupport;
    public Role StoreManager;

    public ObjectAccessPermission CallUserAccountController;

    public DynamicActivityGrant AuthorEditGrant;
    public DynamicActivityGrant ManagerAdjustOrderGrant;

    BooksEntityApp _app; 
    //LoginModule is used to retrieve some authorization roles of login module and add them to book store role(s)
    public BooksAuthorization(BooksEntityApp app, LoginModule loginModule) {
      _app = app; 
      // Data filters
      // 'userid' value will be automatically injected by runtime when evaluating lambdas
      var userDataFilter = new AuthorizationFilter("UserData");
      userDataFilter.Add<IUser, Guid>((u, userId) => u.Id == userId);
      userDataFilter.Add<IBookOrder, Guid>((bo, userId) => bo.User.Id == userId);
      userDataFilter.Add<IBookOrderLine, Guid>((ol, userId) => ol.Order.User.Id == userId);
      userDataFilter.Add<IBookReview, Guid>((r, userId) => r.User.Id == userId);
      // Author data filter. 
      var authorDataFilter = new AuthorizationFilter("AuthorData");
      // Simple case. IAuthor record is matched by filter if IAuthor.User.Id == Current-user-Id
      // Note that IAuthor.User might be null - but it's OK to use 'a.User.Id' - the lambda is rewritten
      // with extra safe conditionals to allow these expressions
      authorDataFilter.Add<IAuthor, Guid>((a, userId) => a.User.Id == userId);

      // More complex case. We provide an expression that checks if userId matches Id of any author of a book.
      // Option 1 - checking book.Authors list, this requires loading list of ALL authors
      //  authorDataFilter.Add<IBook, Guid>((b, userId) => (b.Authors.Any(a => a.User.Id == userId)));
      // Option 2 - Making LINQ query, using Exists<> helper method, it loads a single record
      authorDataFilter.Add<IBook, OperationContext, Guid>((b, ctx, userId) => ctx.Exists<IBookAuthor>(
        ba => ba.Author.User.Id == userId && ba.Book.Id == b.Id));

      // BookOrder filter for adjusting order by StoreManager
      var adjustOrderFilter = new AuthorizationFilter("OrderToAdjust");
      adjustOrderFilter.Add<IBookOrder, Guid>((bo, adjustedOrderId) => bo.Id == adjustedOrderId);
      adjustOrderFilter.Add<IBookOrderLine, Guid>((line, adjustedOrderId) => line.Order.Id == adjustedOrderId);
      // Customer/users data filter. CustomerSupport role allows access only to users who are Customers or Authors (not employees)
      // Here we demo use of data filter expression for pre-filtering users in queries. 
      // When customer support person queries users, the filter 'u.Type==UserType.Customer' will be automatically injected into the LINQ query
      // We also add another filter for authors - the final filter will be OR of both
      var custSupportUserFilter = new AuthorizationFilter("CustomerSupportUsers");
      custSupportUserFilter.Add<IUser>(u => u.Type == UserType.Customer, FilterUse.Entities | FilterUse.Query); 
      // just for testing filter combinations, we add another filter for users that are authors.
      // both filters will be combined (using OR), and whenever customer support person queries users, he will get only Customer and Author users
      var custSupportAuthorUserFilter = new AuthorizationFilter("CustomerSupportAuthorUsers");
      custSupportAuthorUserFilter.Add<IUser>(u => u.Type == UserType.Author, FilterUse.All); 

      //Entity resources
      var books = new EntityGroupResource("Books", typeof(IBook), typeof(IBookAuthor), typeof(IAuthor), typeof(IImage));
      var publishers = new EntityGroupResource("Publishers", typeof(IPublisher));
      var orders = new EntityGroupResource("Orders", typeof(IBookOrder), typeof(IBookOrderLine));
      var reviews = new EntityGroupResource("BookReviews", typeof(IBookReview));
      var users = new EntityGroupResource("Users", typeof(IUser));
      var authorEditData = new EntityGroupResource("AuthorEditData");
      authorEditData.Add(typeof(IAuthor), "Bio");
      authorEditData.Add<IBook>(b => b.Description, b => b.Abstract);
      //authorEditData.Add(typeof(IBook), "Description,Abstract"); //-- alternative way
      var coupons = new EntityGroupResource("Coupons", typeof(ICoupon));
      var couponAppliedOn = new EntityGroupResource("CouponAppliedOn");
      couponAppliedOn.Add<ICoupon>(c => c.AppliedOn);

      //Permissions
      var browseBooks = new EntityGroupPermission("BrowseBooks", AccessType.Read, books, publishers);
      var createOrders = new EntityGroupPermission("CreateOrders", AccessType.CRUD, orders);
      var viewOrders = new EntityGroupPermission("ViewOrders", AccessType.Read, orders);
      var browseReviews = new EntityGroupPermission("BrowseReviews", AccessType.Read, reviews);
      var writeReviews = new EntityGroupPermission("WriteReviews", AccessType.CRUD, reviews);
      var editBooks = new EntityGroupPermission("EditBooks", AccessType.CRUD, books);
      var deleteReviews = new EntityGroupPermission("DeleteReviews", AccessType.Read | AccessType.Delete, reviews);
      var editPublishers = new EntityGroupPermission("EditPublishers", AccessType.CRUD, publishers);
      var editByAuthor = new EntityGroupPermission("EditByAuthor", AccessType.Update, authorEditData);
      var editUsers = new EntityGroupPermission("EditUsers", AccessType.CRUD, users);
      var viewUsers = new EntityGroupPermission("ViewUsers", AccessType.Read, users);
      var adjustOrder = new EntityGroupPermission("AdjustOrder", AccessType.Read | AccessType.Update | AccessType.Delete, orders);

      // We grant Peek permission here for Coupons. The app code can find a coupon by code, but user cannot see it 
      // (no READ permission) or see any coupons. User receives coupon in email, and uses it when buying a book. 
      // System looks up the coupon code and applies the discount. But user cannot never see any coupons in the system.
      var lookupCoupon = new EntityGroupPermission("LookupCoupon", AccessType.Peek, coupons);
      //permission to update ICoupon.AppliedOn property - we set it when user uses coupon
      var useCoupon = new EntityGroupPermission("UseCoupon", AccessType.UpdateStrict, couponAppliedOn);
      var manageCoupons = new EntityGroupPermission("ManageCoupons", AccessType.CRUD, coupons);

      //Activities
      var browsing = new Activity("Browsing", browseBooks, browseReviews);  
      var shopping = new Activity("Shopping", createOrders, lookupCoupon, useCoupon);
      var writingReviews = new Activity("Reviewing", writeReviews);
      var editingUserInfo = new Activity("EditingUserInfo", editUsers);
      var viewingUserInfo = new Activity("ViewingUserInfo", editUsers);
      var viewingOrders = new Activity("ViewingOrders", viewOrders, lookupCoupon);
      var bookEditing = new Activity("BookEditing", editBooks);
      var editingByAuthor = new Activity("EditingByAuthor", editByAuthor);
      var managingStore = new Activity("ManagingStore", editPublishers, manageCoupons);
      var moderatingReviews = new Activity("ModeratingReviews", deleteReviews);
      var adjustingOrders = new Activity("AdjustingOrders", adjustOrder);

      // Controller permissions
      CallUserAccountController = new ObjectAccessPermission("CallUserAccountController", AccessType.ApiAll,
          typeof(Vita.Samples.BookStore.Api.UserAccountController));

      //Roles
      //Browse books and reviews;
      AnonymousUser = new Role("AnonymousUser", browsing);

      // Customer -  view/edit orders only for current user; edit reviews only created by current user 
      Customer = new Role("Customer");
      Customer.ChildRoles.Add(AnonymousUser);
      Customer.Grant(CallUserAccountController);
      Customer.Grant(userDataFilter, shopping, writingReviews, editingUserInfo); 
      BookEditor = new Role("BookEditor", browsing, bookEditing);
      Author = new Role("Author"); 
      Author.ChildRoles.Add(Customer); //author can act as a customer and buy a book
      //We save the grant in static field, to explicitly enable the activity at runtime for limited scope  
      AuthorEditGrant = Author.GrantDynamic(authorDataFilter, editingByAuthor);
      //Customer support can view orders and users (only users that are customers!)
      CustomerSupport = new Role("CustomerSupport", viewingOrders);
      CustomerSupport.Grant(custSupportUserFilter, viewingUserInfo);
      CustomerSupport.Grant(custSupportAuthorUserFilter, viewingUserInfo);
      //Store manager
      StoreManager = new Role("StoreManager", browsing, managingStore, moderatingReviews);
      // Store manager is able to adjust orders, but only in the context of dynamic (explicitly started) activity
      // When adjusting activity starts, it saves order Id in user context under AdjustedOrderIdKey. 
      // All records being edited are then verified against this order Id.
      // This garantees that during adjustment editing we modify only data for the order that we started adjustment for.
      ManagerAdjustOrderGrant = StoreManager.GrantDynamic(adjustOrderFilter, adjustingOrders, AdjustedOrderIdKey); 
      //Add permission to access LoginAdministration API controller
      StoreManager.ChildRoles.Add(loginModule.Authorization.LoginAdministrator);
      StoreManager.ChildRoles.Add(Vita.Modules.Logging.LoggingAuthorizationRoles.Instance.LogDataViewerRole);
    }

    public IList<Role> GetRoles(UserType userType) {
      var roles = new List<Role>();
      if(userType.IsSet(UserType.Customer))
        roles.Add(Customer);
      if(userType.IsSet(UserType.Author))
        roles.Add(Author);
      if(userType.IsSet(UserType.BookEditor))
        roles.Add(BookEditor);
      if(userType.IsSet(UserType.CustomerSupport))
        roles.Add(CustomerSupport);
      if(userType.IsSet(UserType.StoreAdmin))
        roles.Add(StoreManager);
      return roles;
    }


  }//class
}
