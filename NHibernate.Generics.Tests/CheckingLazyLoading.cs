﻿#region license
// Copyright (c) 2005 - 2007 Ayende Rahien (ayende@ayende.com)
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice,
//     this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice,
//     this list of conditions and the following disclaimer in the documentation
//     and/or other materials provided with the distribution.
//     * Neither the name of Ayende Rahien nor the names of its
//     contributors may be used to endorse or promote products derived from this
//     software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
// THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion


using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace NHibernate.Generics.Tests
{
    [TestFixture]
    public class CheckingLazyLoading
    {

        Blog blog;
        Post post1, post2;
        ISessionFactory factory;
        ISession session;
        
        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            factory = DatabaseTests.CreateSessionFactory();
        }

        [SetUp]
        public void Setup()
        {
            blog = new Blog();
            post1 = new Post();
            post2 = new Post();
            post1.Blog = blog;
            post2.Blog = blog;

            session = factory.OpenSession();
            session.Save(blog);
            session.Save(post1);
            session.Save(post2);
            session.Dispose();

            session = factory.OpenSession();
        }

        [TearDown]
        public void TearDown()
        {
            session.Dispose();
        }

        [Test]
        public void LazyLoadingCollection()
        {
            Blog fromDb = (Blog )session.Load(typeof(Blog),blog.BlogID);
            Assert.IsFalse(
                NHibernateUtil.IsInitialized(((IWrapper)fromDb.Posts).Value)
            );
            int i = fromDb.Posts.Count;//init collection, don't ever do that on production!
            Assert.IsTrue(
                NHibernateUtil.IsInitialized(((IWrapper)fromDb.Posts).Value)
            );
        }

        [Test]
        public void LazyLoadingProperty()
        {
            Post fromDb = (Post)session.Load(typeof(Post), post2.PostId);
            Assert.IsFalse(
               NHibernateUtil.IsInitialized(fromDb.Blog)
           );
            string name = fromDb.Blog.BlogName;
            Assert.IsTrue(
               NHibernateUtil.IsInitialized(fromDb.Blog)
           );
        }
    
    
    }
}
