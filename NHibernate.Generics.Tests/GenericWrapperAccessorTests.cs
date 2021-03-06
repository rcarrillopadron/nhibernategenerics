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
using NHibernate.Tool.hbm2ddl;
using NUnit.Framework;
using System.Reflection;
using System.Collections;
using NHibernate.Generics;

namespace NHibernate.Generics.Tests
{
	[TestFixture]
	public class DatabaseTests
	{
		ISessionFactory factory;
		ISession session;
		
		[TestFixtureSetUp]
		public void FixtureSetup()
		{
			log4net.Config.XmlConfigurator.Configure();
			factory = CreateSessionFactory();
		}

		public static NHibernate.Cfg.Configuration CreateConfiguration()
		{
			IDictionary<string, string> props = new Dictionary<string, string>();
			props["connection.driver_class"] = "NHibernate.Driver.SQLite20Driver";
			props["dialect"] = "NHibernate.Dialect.SQLiteDialect";
			props["connection.provider"] = "NHibernate.Connection.DriverConnectionProvider";
			props["connection.connection_string"] =
				string.Format(@"Data Source=|DataDirectory|\{0};Version=3;New=True", "NHibernate.Generics.Test.SQLite");
			NHibernate.Cfg.Configuration cfg = new NHibernate.Cfg.Configuration();
            cfg.Properties = props as IDictionary;
            cfg.AddAssembly(Assembly.GetExecutingAssembly());
			return cfg;
		}
		
		public static ISessionFactory CreateSessionFactory()
		{
			NHibernate.Cfg.Configuration cfg = CreateConfiguration();
			ISessionFactory factory = cfg.BuildSessionFactory();
			new SchemaExport(cfg).Create(true,true);
			return factory;
		}

		[SetUp]
		public void Setup()
		{
			session = factory.OpenSession();
		}

		[TearDown]
		public void Teardown()
		{
			session.Dispose();
		}

		[Test]
		public void LazyLoadingWhenAddingAPost()
		{
			Blog blog = new Blog("My Blog");
			//Need to save it first, so it would exist in the DB
			//when saving the post
			session.Save(blog);
			session.Flush();
			Post post = new Post("First Post");
			blog.Posts.Add(post);
			session.Save(post);
			session.Dispose();
			session = factory.OpenSession();
			Blog fromDb = (Blog)session.Load(typeof(Blog), blog.BlogID);
			AssertPostCollectionIsLazy(fromDb);
			Post newPost = new Post("Second Post");
			fromDb.Posts.Add(newPost);
			session.Save(newPost);
			AssertPostCollectionIsLazy(fromDb);
		}

		private static void AssertPostCollectionIsLazy(Blog fromDb)
		{
			Assert.IsFalse(
				NHibernateUtil.IsInitialized(((IWrapper)fromDb.Posts).Value),
				"Lazy loading doesn't work??"
			);
		}

		[Test]
		public void AddBlogToPostAndSave()
		{
			Blog blog = new Blog("My Blog");
			//Need to save it first, so it would exist in the DB
			//when saving the post
			session.Save(blog);
			session.Flush();
			Post post = new Post("First Post");
			blog.Posts.Add(post);
			session.Save(post);

			session.Flush();
			session.Dispose();

			//Needs to create a new session so I would get the same instance
			session = factory.OpenSession();

			Blog blogFromDB = (Blog)session.Load(typeof(Blog), blog.BlogID);
			Post postFromDB = (Post)session.Load(typeof(Post), post.PostId);

			Assert.AreEqual(blogFromDB, postFromDB.Blog);
			Assert.IsTrue(blogFromDB.Posts.Contains(postFromDB));
			
		}

		[Test]
		public void AssignBlogToPostPersistWhilePostsIsNotLoadedDoesNotLoad()
		{
			Blog blog = new Blog("My Blog");
			session.Save(blog);
			session.Flush();
			Post post = new Post("First Post");
			blog.Posts.Add(post);
			session.Save(post);

			session.Flush();
			session.Dispose();

			//Needs to create a new session so I would get the same instance
			session = factory.OpenSession();

			Blog blogFromDB = (Blog)session.Load(typeof(Blog), blog.BlogID);
			EntitySet<Post> posts = (EntitySet<Post>)blogFromDB.Posts;
			Assert.IsFalse(posts.IsInitialized);
			Post newPost = new Post("Second Post");
			newPost.Blog = blogFromDB;
			Assert.IsFalse(posts.IsInitialized);
		}

		[Test]
		public void AddToPostsDoesNotLoadsThem()
		{
			Blog blog = new Blog("My Blog");
			session.Save(blog);
			session.Flush();
			Post post = new Post("First Post");
			blog.Posts.Add(post);
			session.Save(post);

			session.Flush();
			session.Dispose();

			//Needs to create a new session so I would get the same instance
			session = factory.OpenSession();

			Blog blogFromDB = (Blog)session.Load(typeof(Blog), blog.BlogID);
			EntitySet<Post> posts = (EntitySet<Post>)blogFromDB.Posts;
			Assert.IsFalse(posts.IsInitialized);
			Post newPost = new Post("Second Post");
			blogFromDB.Posts.Add(newPost);
			Assert.IsFalse(posts.IsInitialized);


			//Need to save before changes will appear
			session.Save(newPost);
			session.Flush();
			Assert.IsTrue(blogFromDB.Posts.Contains(newPost));
		}

		/// <summary>
		/// This test is to verify that you can't do this:
		/// Blog blog = ...;
		/// Post post = ...;
		/// 
		/// // Posts is lazy loaded, so it's not loaded
		/// // even though there is an addition
		/// blog.Posts.Add(post);
		/// 
		/// //This loads the collection as it is IN THE DATABASE!
		/// //Because we didn't saved, the post won't appear here!
		/// blog.Posts.Contains(post); //Will return FALSE
		/// </summary>
		[Test]
		public void CantGetAddedItemsFromLazyLoadCollectionIfNotSavedToDB()
		{
			Blog blog = new Blog("My Blog");
			session.Save(blog);
			session.Flush();
			Post post = new Post("First Post");
			blog.Posts.Add(post);
			session.Save(post);

			session.Flush();
			session.Dispose();

			//Needs to create a new session so I would get the same instance
			session = factory.OpenSession();

			Blog blogFromDB = (Blog)session.Load(typeof(Blog), blog.BlogID);
			EntitySet<Post> posts = (EntitySet<Post>)blogFromDB.Posts;
			Post newPost = new Post("Second Post");
			blogFromDB.Posts.Add(newPost);
			Assert.IsFalse(posts.IsInitialized);
			Assert.IsFalse(blogFromDB.Posts.Contains(newPost));
			Assert.IsTrue(posts.IsInitialized);
		}

		/// <summary>
		/// This test is to verify that you can do this:
		/// Blog blog = ...;
		/// Post post = ...;
		/// 
		/// // Posts is lazy loaded, so it's not loaded
		/// // even though there is an addition
		/// blog.Posts.Add(post);
		/// session.Save(post);
		/// session.Flush();//Save the post to database
		/// 
		/// blog.Posts.Contains(post); //Will return True
		/// </summary>
		[Test]
		public void CanGetNewlyAddedItemInLazyCollectionIfFlushedToDB()
		{
			Blog blog = new Blog("My Blog");
			session.Save(blog);
			session.Flush();
			Post post = new Post("First Post");
			blog.Posts.Add(post);
			session.Save(post);

			session.Flush();
			session.Dispose();

			//Needs to create a new session so I would get the same instance
			session = factory.OpenSession();

			Blog blogFromDB = (Blog)session.Load(typeof(Blog), blog.BlogID);
			EntitySet<Post> posts = (EntitySet<Post>)blogFromDB.Posts;
			Post newPost = new Post("Second Post");
			blogFromDB.Posts.Add(newPost);
			session.Save(newPost);
			//session.Flush();
			Assert.IsFalse(posts.IsInitialized);
			Assert.IsTrue(blogFromDB.Posts.Contains(newPost));
			Assert.IsTrue(posts.IsInitialized);
		}

		[Test]
		public void LoadCollectionAndThenAddingToItAddsToInMemoryCollection()
		{
			Blog blog = new Blog("My Blog");
			session.Save(blog);
			session.Flush();
			Post post = new Post("First Post");
			blog.Posts.Add(post);
			session.Save(post);

			session.Flush();
			session.Dispose();

			//Needs to create a new session so I would get the same instance
			session = factory.OpenSession();

			Blog blogFromDB = (Blog)session.Load(typeof(Blog), blog.BlogID);
			EntitySet<Post> posts = (EntitySet<Post>)blogFromDB.Posts;
			posts.Load();//Load the lazy collection.
			Assert.IsTrue(posts.IsInitialized);
			Post newPost = new Post("Second Post");
			blogFromDB.Posts.Add(newPost);
			Assert.IsTrue(blogFromDB.Posts.Contains(newPost));
			Assert.IsTrue(posts.IsInitialized);
		}

		[Test]
		public void AddCommentToPost_SaveAndLoad()
		{
			Blog blog = new Blog("My Blog");
			session.Save(blog);
			session.Flush();
			Post post = new Post("First Post");
			blog.Posts.Add(post);
			post.Comments.Add(new Comment("First Comment"));
			post.Comments.Add(new Comment("Second Comment"));
			session.Save(post);

			session.Flush();
			session.Dispose();

			//Needs to create a new session so I would get the same instance
			session = factory.OpenSession();

			Post fromDb = (Post)session.Load(typeof(Post), post.PostId);
			Assert.AreEqual(2, fromDb.Comments.Count);

			Assert.AreEqual("First Comment", fromDb.Comments[0].Text);
			Assert.AreEqual("Second Comment", fromDb.Comments[1].Text);

		}
	}
}
