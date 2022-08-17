using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DEVCOMP.APP.SRV.WebServices.Model;
using System.ServiceModel;
using DEVCOMP.APP.SRV.WebServices.Extensions;
using System.Data.Entity.Validation;

namespace DEVCOMP.APP.SRV.WebServices
{
    public class SRVProvider
    {

        #region Main methods, saving request in DB. SaveRequestRecord is called 1 or 2 times from UpdateRegistryRequest
        private long SaveRequestRecord(SRV_REQUEST request, SRV_REGTYPE registryType, SRV context, bool traverseDual)
        {
            request.SRV_REGTYPEID = registryType.ID;
            request.LOADDATE = DateTime.Now;
            
            decimal id = ServiceProvider.GetNewId();
            request.SRV_REQID = (long)id;

            short routeTypeID = request.SRV_REQROUTETYPEID.GetValueOrDefault(2);
            if (routeTypeID < 1 || routeTypeID > 2) { routeTypeID = 2; } // force default value
            request.SRV_REQROUTETYPEID = routeTypeID;

            #region Specific cases
            if (request.SRV_ENT1 != null)
            {
                request.SRV_ENT1.SRV_REQID = (long)id;

                foreach (SRV_PERS1 pers1 in request.SRV_ENT1.SrvPersons1.OrEmptyIfNull())
                {
                    pers1.ID = (long)ServiceProvider.GetNewId();
                    pers1.SRV_ENT1 = request.SRV_ENT1;
                }

                foreach (SRV_ENT2 ent2 in request.SRV_ENT1.SRVEnt2s.OrEmptyIfNull())
                {
                    ent2.ID = (long)ServiceProvider.GetNewId();
                    ent2.SRV_ENT1 = request.SRV_ENT1;
                }
            }
            if (request.SRV_SIMPL != null)
            {
                request.SRV_SIMPL.SRV_REQID = (long)id;
            }
            #endregion

            #region Contacts
            if (request.SRV_CONTACT != null)
            {
                request.SRV_CONTACT.SRV_REQUEST = new List<SRV_REQUEST>();
                request.SRV_CONTACT.SRV_REQUEST.Add(request);
                request.SRV_CONTACT.ID = (long)ServiceProvider.GetNewId();
                request.SRV_CONTACTID = request.SRV_CONTACT.ID;

                PrepareContact(request.SRV_CONTACT);
            }

            if (request.COMPCONTACT != null)
            {
                request.COMPCONTACT.COMPREQ = new List<SRV_REQUEST>();
                request.COMPCONTACT.COMPREQ.Add(request);
                request.COMPCONTACT.ID = (long)ServiceProvider.GetNewId();
                request.COMPCONTACTID = request.COMPCONTACT.ID;

                PrepareContact(request.COMPCONTACT);
            }


            foreach (SRV_PERSON person in request.SRVPersons.OrEmptyIfNull())
            {
                person.SRV_REQID = (long)id;
                PreparePerson(person, context);
            }


            foreach (SRV_EXPERT expert in request.SRVExperts.OrEmptyIfNull())
            {
                expert.ID = (long)ServiceProvider.GetNewId();
                expert.SRV_REQID = (long)id;
                expert.SRV_REQUEST = request;

                if (expert.SRV_IDCARD != null)
                {
                    expert.SRV_IDCARD.ID = (long)ServiceProvider.GetNewId();
                    expert.SRV_IDCARDID = expert.SRV_IDCARD.ID;
                    expert.SRV_IDCARD.SRV_EXPERT = new List<SRV_EXPERT> { expert };
                }
            }

            foreach (SRV_PERSON2 person2 in request.SRVPersons2.OrEmptyIfNull())
            {
                person2.ID = (long)ServiceProvider.GetNewId();
                person2.SRV_REQID = (long)id;
                if (person2.SRV_PERSON != null)
                {
                    person2.SRV_PERSON.ID = (long)ServiceProvider.GetNewId();
                    person2.CHIEFID = person2.SRV_PERSON.ID;
                    person2.SRV_PERSON.SRVPersons2 = new List<SRV_PERSON2>();
                    person2.SRV_PERSON.SRVPersons2.Add(person2);
                }
            }


            #endregion Branches
            foreach (SRV_BRANCH branch in request.SrvBranches.OrEmptyIfNull())
            {
                branch.ID = (long)ServiceProvider.GetNewId();
                branch.SRV_REQID = (long)id;
                if (branch.SRV_CONTACT != null)
                {
                    branch.SRV_CONTACT.ID = (long)ServiceProvider.GetNewId();
                    branch.SRV_CONTACTID = branch.SRV_CONTACT.ID;
                    branch.SRV_CONTACT.SRV_BRANCH = new List<SRV_BRANCH>();
                    branch.SRV_CONTACT.SRV_BRANCH.Add(branch);

                    PrepareContact(branch.SRV_CONTACT);
                }
                
                foreach (SRV_EXPERT expert in branch.SRVExperts.OrEmptyIfNull())
                {
                    if (expert.SRV_REQUEST == null && (expert.ID <= 0))
                    {
                        expert.ID = (long)ServiceProvider.GetNewId();
                    }
                    expert.SRV_BRANCHID = branch.ID;
                    expert.SRV_BRANCH = branch;

                    if (expert.SRV_IDCARD != null)
                    {
                        expert.SRV_IDCARD.ID = (long)ServiceProvider.GetNewId();
                        expert.SRV_IDCARDID = expert.SRV_IDCARD.ID;
                        expert.SRV_IDCARD.SRV_EXPERT = new List<SRV_EXPERT> { expert };
                    }
                }
                foreach (SRV_PERSON person in branch.SRVPersons.OrEmptyIfNull())
                {
                    person.PersonsSRVBranch = branch;
                    person.SRV_BRANCHID = branch.ID;
                    PreparePerson(person, context);
                }
            }
            #endregion


            #region Common types

            if (request.SRV_PERSONCATID.HasValue)
            {
                try
                {
                    SRV_PERSONCAT category = context.FindObject<SRV_PERSONCAT>(request.SRV_PERSONCATID.Value);
                }
                catch
                {
                    throw new ApplicationException("Wrong person category ID: " + request.SRV_PERSONCATID.ToString());
                }
            }

            // skip ~200 lines
            #endregion

            #region Notes
            foreach (SRV_NOTE note in request.SrvNotes.OrEmptyIfNull())
            {
                note.SRV_REQID = (long)id;
                note.ID = (long)ServiceProvider.GetNewId();
            }

            #endregion

            context.SRV_REQUEST.Add(request);
            return (long)id;

        }

        private short GetEquipKindID(short externalKindID, SRV context)
        {
            var query = from k in context.SRV_EQUIPKIND where k.EXTERNAL_KIND == externalKindID select k;
            SRV_EQUIPKIND kind = query.FirstOrDefault();
            if (kind == null)
                throw new ApplicationException("Wrong equipkind: " + externalKindID.ToString());
            return kind.ID; 
        }

        public void UpdateRegistryRequest(SRV_REQUEST request, string registryCode)
        {
            try
            {
                SRV context = DataAccessHelper.GetDbContext<SRV>();

                SRV_REGTYPE registryType = null;
                SRV_REQUEST dualRequest = null;

                // A request in reply to another request
                if (request.SRV_REQTYPEID == 24)
                {
                    SRV_REQUEST initialRequest = GetRequestByDocID(context, request.REFDOCID);
                    
                    registryType = request.SRV_REGTYPE;
                    var query = from r in context.SRV_REGTYPE where r.ID == initialRequest.SRV_REGTYPEID select r;
                    registryType = query.FirstOrDefault();
                    request.SRV_REGTYPE = registryType;

                    request.OTHERREQID = initialRequest.SRV_REQID;
                }
                else
                {
                    registryType = GetRegType(registryCode);
                    if (registryType == null)
                        throw new ApplicationException("Registry type " + registryCode + " not found");

                    if (request.DualRequest != null)
                    {

                        dualRequest = request.DualRequest.FirstOrDefault();
                        if (dualRequest != null)
                        {
                            request.DualRequest = null;
                            request.OTHERREQID = null;
                        }

                    }
                }

                long requestID = SaveRequestRecord(request, registryType, context, false);

                context.SaveChanges();

                if (dualRequest != null && request.SRV_REQTYPEID != 24)
                {

                    long dualRequestID = SaveRequestRecord(dualRequest, registryType, context, false);
                    dualRequest.OTHERREQID = requestID;

                    context.SaveChanges();


                }

                #region Information about successful operation
                SRV_STATE status = new SRV_STATE();
                status.ID = (long)ServiceProvider.GetNewId();
                status.SRV_STATEKINDID = 0;
                status.SRV_REQID = (dualRequest == null)?requestID:dualRequest.SRV_REQID;
                status.SENTTOSRV = 0;
                status.SDATE = DateTime.Now;
                context.SRV_STATE.Add(status);
                context.SaveChanges();
                #endregion
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.GetDescription());
            }
        }
        #endregion


        #region Supplemental methods
        // SRV_REGTYPE
        public SRV_REGTYPE GetRegType(string code)
        {
            try
            {
                SRV context = DataAccessHelper.GetDbContext<SRV>();
                var query = from r in context.SRV_REGTYPE where r.CODE == code select r;
                return query.FirstOrDefault();
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.GetDescription());
            }
        }

        // SRV_OWNERTYPE *2
        private void FixOwnerType(IOwnerType store)
        {
            try
            {
                if ( store.SRV_OWNERTYPE != null)
                {
                    SRV_OWNERTYPE requestedType = GetOwnerType(store.SRV_OWNERTYPE.OWNERTYPENAME);
                    store.SRV_OWNERTYPEID = requestedType.ID;
                    store.SRV_OWNERTYPE = null;
                }
            }
            catch
            {
                store.SRV_OWNERTYPE.ID = (long)ServiceProvider.GetNewId();
                store.SRV_OWNERTYPEID = store.SRV_OWNERTYPE.ID;
            }
        }

        private SRV_OWNERTYPE GetOwnerType(string name)
        {
            return DataAccessHelper.GetDbContext<SRV>().FindObject<SRV_OWNERTYPE>((p => p.OWNERTYPENAME == name), "owner.NAME == " + name);
        }

        // SRV_CONTACT + children
        private void PrepareContact(SRV_CONTACT contact)
        {
            if (contact.POSTADDR != null)
            {
                contact.POSTADDR.PostAddr = new List<SRV_CONTACT>() { contact };
                contact.POSTADDR.ID = (long)ServiceProvider.GetNewId();
                contact.POSTALADDR = contact.POSTADDR.ID;
            }

            if (contact.LEGALADDR != null)
            {
                contact.LEGALADDR.LegalAddr = new List<SRV_CONTACT>() { contact };
                contact.LEGALADDR.ID = (long)ServiceProvider.GetNewId();
                contact.LEGALADDR = contact.LEGALADDR.ID;
            }

            foreach (SRV_PHONE phone in contact.SRVPhones.OrEmptyIfNull())
            {
                phone.ID = (long)ServiceProvider.GetNewId();
                phone.SRV_CONTACTID = contact.ID;
                phone.SRV_CONTACT = contact;
            }

            foreach (SRV_EMAIL email in contact.SRVEmails.OrEmptyIfNull())
            {
                email.ID = (long)ServiceProvider.GetNewId();
                email.SRV_CONTACTID = contact.ID;
                email.SRV_CONTACT = contact;
            }
        }

        // SRV_PERSON + children
        private void PreparePerson(SRV_PERSON person, SRV context)
        {
            person.ID = (long)ServiceProvider.GetNewId();
            

            if (person.Addr != null)
            {
                person.Addr.ID = (long)ServiceProvider.GetNewId();
                person.SRV_ADDRID = person.Addr.ID;
                context.SRV_ADDR.Add(person.Addr);
                
            }

            foreach (SRV_PERSON subordinate in person.Subordinates.OrEmptyIfNull())
            {
                subordinate.ID = (long)ServiceProvider.GetNewId();
                subordinate.Head = person;
                subordinate.HEADID = person.ID;
                //subordinate.SRV_REQID = (long)id;

                if (subordinate.Addr != null)
                {
                    subordinate.Addr.ID = (long)ServiceProvider.GetNewId();
                    subordinate.SRV_ADDRID = subordinate.Addr.ID;
                    context.SRV_ADDR.Add(subordinate.Addr);
                }
            }
        }


        private long CheckDict<T> (long id) where T:class, ILongCodeObject, new()
        {
            SRV context = DataAccessHelper.GetDbContext<SRV>();
            try {
                T dictObject = context.FindObject<T>(id);
                return id;
            }
            catch 
            {
                T newObject = new T();
                newObject.ID = (long)ServiceProvider.GetNewId();
                context.Set<T>().Add(newObject);
                return newObject.ID;
            }
        }
        

        private SRV_REQUEST GetRequestByDocID(SRV context, string refDocID)
        {
            var query = from m in context.SRV_MSG where m.DOCID == refDocID && m.DIR == 2 && m.TYPE == "NtEvt" select m;
            SRV_MSG notQuery = query.FirstOrDefault();

            if (notQuery != null)
            {
                var requestQuery = from r in context.SRV_REQUEST where r.DOCID == notQuery.REFDOCID && r.SRV_REQTYPEID != 2 select r;
                return requestQuery.FirstOrDefault();
            }
            else
                throw new ApplicationException("Not found additional information request for documentID = "+refDocID);
        }

        #endregion

        private long? GetCountryIDByCountryCode(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode))
                return null;

            SRV context = DataAccessHelper.GetDbContext<SRV>();
            var countryQuery = from c in context.COMP_COUNTRY where c.CODE2.ToUpper() == countryCode.ToUpper() select c.ID;
            return countryQuery.FirstOrDefault(); 
        }

        /* template
        public SRV_REGTYPE GetRegType(string code)
        {
            try 
            {
                SRV context = DataAccessHelper.GetDbContext<SRV>();
            }
            catch (Exception ex)
            {
                throw new FaultException(ex.GetDescription());
            }
        }
         */
    }
}
