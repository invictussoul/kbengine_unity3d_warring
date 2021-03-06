using UnityEngine;
using KBEngine;
using System.Collections;
using System;
using System.Xml;
using System.Collections.Generic;

public class AssetLoad
{
	public WWW www = null;
	public LoadAssetsPool pool = null;
	loader loader_ = null;
	public Asset asset = null;
	List<SceneObject> watcherobjs = new List<SceneObject>();
	
	public AssetLoad() {  
	}
	
	public AssetLoad(loader l, LoadAssetsPool threadpool, Asset a) {  
		asset = a;
		a.load = this;
		pool = threadpool;
		loader_ = l;
	}
	
	public void addWatcher(SceneObject obj)
	{
		watcherobjs.Add(obj);
	}

	public void delWatcher(SceneObject obj)
	{
		for(int i=0; i<watcherobjs.Count; i++)
		{
			if(watcherobjs[i] == obj){
				watcherobjs.RemoveAt(i);
				return;
			}
		}
	}
	
    public bool loading {  
	    get{
	    	return asset.loading;
	    }  
        set{
        	asset.loading = value;
        }
    }  
            
	IEnumerator _load()
	{
__RESTART:
		Common.DEBUG_MSG("AssetLoad::_load: starting load(" + Common.safe_url("/StreamingAssets/" + asset.source) + ")...");
		www = new WWW(Common.safe_url("/StreamingAssets/" + asset.source));  
		yield return www; 
		
		watcherobjs.Clear();
		
		if(www != null)
		{
	        if (www.error != null)
	        {
	            Common.ERROR_MSG(www.error);  
	            goto __RESTART;
			}
			else
			{
				Common.DEBUG_MSG("AssetLoad::_load: (" + Common.safe_url("/StreamingAssets/" + asset.source) + ") is finished!");
				asset.load = null;
				
				if(loading)
				{
					loading = false;
					asset.bundle = www.assetBundle;
					if(asset.bundle != null)
					{
						asset.onLoadCompleted();
						onLoadCompleted();
					}
					else
					{
						Common.DEBUG_MSG("AssetLoad::_load: (" + Common.safe_url("/StreamingAssets/" + asset.source) + ") bundle is null!");
					}
				}
			}
		}
		
		pool.onLoadCompleted(this);
	}
	
	public virtual void onLoadCompleted()
	{
	}
	
	public bool start()
	{
		if(loading == true)
			return false;
		
		loading = true;
		loader_.StartCoroutine(_load());
		return true;
	}
	
	public void stop()
	{
		loading = false;
		www = null;
	}
}

public class AssetLoadSkybox : AssetLoad
{
	public AssetLoadSkybox(loader l, LoadAssetsPool threadpool, Asset a):
		base(l, threadpool, a)
	{  
	}
	
	public override void onLoadCompleted()
	{
		RenderSettings.skybox = (Material)asset.bundle.mainAsset;
		Common.DEBUG_MSG("AssetLoadSkybox::onLoadCompleted: skybox(" + asset.bundle.mainAsset.name + ")! inst=" + RenderSettings.skybox);
	}
}

public class AssetLoadTerrain : AssetLoad
{
	public AssetLoadTerrain(loader l, LoadAssetsPool threadpool, Asset a):
		base(l, threadpool, a)
	{  
	}
	
	public override void onLoadCompleted()
	{
		loader.inst.StartCoroutine(_onTerrainLoadCompoleted());
	}
	
	IEnumerator _onTerrainLoadCompoleted()
	{
		Scene scene = loader.inst.findScene(loader.inst.currentSceneName, false);
		if(scene == null && scene.worldmgr != null)
		{
			Common.ERROR_MSG("AssetLoadTerrain::onLoadCompleted: not found scene! name=" + asset.source);
			yield break;
		}
		else
		{
			asset.Instantiate(scene.worldmgr, "");
		}
	}
}

public class LoadAssetsPool 
{
	private int maxthread_ = 1;
	private int currLoadings = 0;
	
	private loader loader_ = null;
	
	private List<AssetLoad> before_loads = new List<AssetLoad>();
	private List<AssetLoad> after_loads = new List<AssetLoad>();
	private List<AssetLoad> loads = new List<AssetLoad>();
	
	public bool isLoading = false;
	
	public LoadAssetsPool(loader l, int maxthread) {  
		maxthread_ = maxthread;
		loader_ = l;
	}

	public void onLoadCompleted(AssetLoad dl)
	{
		currLoadings -= 1;

		if(dl.asset.loadLevel == Asset.LOAD_LEVEL.LEVEL_ENTER_BEFORE)
		{
			loadingbar.loadingbar_currValue += 1;
			Common.DEBUG_MSG("loadingbar_currValue=" + loadingbar.loadingbar_currValue);
			for(int i=0; i<before_loads.Count; i++)
			{
				if(before_loads[i] == dl)
				{
					before_loads.RemoveAt(i);
					break;
				}
			}
			
			if(before_loads.Count == 0)
			{
				Common.DEBUG_MSG("LoadAssetsPool::onLoadCompleted: ENTER_BEFORE!");
			}
		}
		else if(dl.asset.loadLevel == Asset.LOAD_LEVEL.LEVEL_ENTER_AFTER)
		{
			for(int i=0; i<after_loads.Count; i++)
			{
				if(after_loads[i] == dl)
				{
					after_loads.RemoveAt(i);
					break;
				}
			}
			
			if(after_loads.Count == 0)
			{
				Common.DEBUG_MSG("LoadAssetsPool::onLoadCompleted: ENTER_AFTER!");
			}
		}
		else
		{
			for(int i=0; i<loads.Count; i++)
			{
				if(loads[i] == dl)
				{
					loads.RemoveAt(i);
					break;
				}
			}
			
			if(loads.Count == 0)
			{
				Common.DEBUG_MSG("LoadAssetsPool::onLoadCompleted: NORMAL!");
			}
		}
		
		loadNext();
	}
	
	public void add(AssetLoad load)
	{
		// Common.DEBUG_MSG("LoadAssetsPool::add: " + load.asset.source + ", loadlevel=" + load.asset.loadLevel + "!");
		switch(load.asset.loadLevel)
		{
			case Asset.LOAD_LEVEL.LEVEL_ENTER_BEFORE:
				before_loads.Add(load);
				loadingbar.loadingbar_maxValue += 1;
				break;
			case Asset.LOAD_LEVEL.LEVEL_ENTER_AFTER:
				after_loads.Add(load);
				break;
			case Asset.LOAD_LEVEL.LEVEL_SCRIPT_DYNAMIC:
				// after_loads.Add(load);
				break;
			default:
				loads.Add(load);
				break;
		};
	}
	
	public void start()
	{	
		Common.DEBUG_MSG("LoadAssetsPool::start: currLoadings=" + currLoadings + ", isLoading=" + isLoading
		 + ", before_loads=" + before_loads.Count + ", after_loads=" + after_loads.Count + ", loads=" + loads.Count);
		isLoading = true;
		sortPri();
		
		if(loadingbar.inst != null)
		{
			loadingbar.inst.enable();
		}
		
		if(currLoadings >= maxthread_)
			return;
		
		for(int i=0; i<maxthread_ - currLoadings; i++)
		{
			loadNext();
		}
	}
	
	public void loadNext()
	{
		for(int i=0; i<before_loads.Count; i++)
		{
			if(before_loads[i].loading == false)
			{
				currLoadings += 1;
				before_loads[i].start();
				return;
			}
		}
		
		for(int i=0; i<after_loads.Count; i++)
		{
			if(after_loads[i].loading == false)
			{
				currLoadings += 1;
				after_loads[i].start();
				return;
			}
		}
			
		for(int i=0; i<loads.Count; i++)
		{
			if(loads[i].loading == false)
			{
				currLoadings += 1;
				loads[i].start();
				return;
			}
		}
		
		isLoading = false;
	}
	
	public void sortPri()
	{
		before_loads.Sort(delegate(AssetLoad x, AssetLoad y) { return x.asset.loadPri - y.asset.loadPri; });
		after_loads.Sort(delegate(AssetLoad x, AssetLoad y) { return x.asset.loadPri - y.asset.loadPri; });
		loads.Sort(delegate(AssetLoad x, AssetLoad y) { return x.asset.loadPri - y.asset.loadPri; });
	}
	
	public bool removeLoad(string source)
	{
		for(int i=0; i<before_loads.Count; i++)
		{
			if(before_loads[i].asset.source == source)
			{
				if(before_loads[i].loading == true)
					return false;
				
				before_loads.RemoveAt(i);
				return true;
			}
		}
		
		for(int i=0; i<after_loads.Count; i++)
		{
			if(after_loads[i].asset.source == source)
			{
				if(after_loads[i].loading == true)
					return false;
				
				after_loads.RemoveAt(i);
				return true;
			}
		}
			
		for(int i=0; i<loads.Count; i++)
		{
			if(loads[i].asset.source == source)
			{
				if(loads[i].loading == true)
					return false;
				
				loads.RemoveAt(i);
				return true;
			}
		}
		
		return true;
	}
	
	public AssetLoad findLoad(string source)
	{
		for(int i=0; i<before_loads.Count; i++)
		{
			if(before_loads[i].asset.source == source)
			{
				return before_loads[i];
			}
		}
		
		for(int i=0; i<after_loads.Count; i++)
		{
			if(after_loads[i].asset.source == source)
			{
				return after_loads[i];
			}
		}
			
		for(int i=0; i<loads.Count; i++)
		{
			if(loads[i].asset.source == source)
			{
				return loads[i];
			}
		}
		
		return null;
	}
	
	public AssetLoad addLoad(Asset a)
	{
		if(a.bundle != null || a.isLoaded == true)
		{
			Common.DEBUG_MSG("LoadAssetsPool::addLoad:" + a.source + ", bundle != null(" + 
				a.bundle + ") or isLoaded(" + a.isLoaded + ")!");
			return null;
		}
		
		if(findLoad(a.source) != null)
		{
			Common.DEBUG_MSG("LoadAssetsPool::addLoad: findLoad("+ a.source + ") = true!");
			return null;
		}
		
		AssetLoad load = null;
		
		if(a.type == Asset.TYPE.SKYBOX)
		{
			load = new AssetLoadSkybox(loader_, this, a);
		}
		else if(a.type == Asset.TYPE.TERRAIN)
		{
			load = new AssetLoadTerrain(loader_, this, a);
		}
		else if(a.type == Asset.TYPE.TERRAIN_DETAIL_TEXTURE)
		{
			load = new AssetLoadTerrain(loader_, this, a);
		}
		else if(a.type == Asset.TYPE.TERRAIN_TREE)
		{
			load = new AssetLoadTerrain(loader_, this, a);
		}
		else if(a.type == Asset.TYPE.WORLD_OBJ)
		{
			load = new AssetLoad(loader_, this, a);
		}
		else
		{
			load = new AssetLoad(loader_, this, a);
		}
		
		add(load);
		return load;
	}
	
	public bool stop(bool isAllUnLoadLevel)
	{
		// loader_.StopAllCoroutines();

		for(int i=0; i<before_loads.Count; i++)
		{
			AssetLoad load = before_loads[i];
			if(isAllUnLoadLevel == false && load.asset.unloadLevel != Asset.UNLOAD_LEVEL.LEVEL_NORMAL)
				continue;
				
			if(load.loading == true)
			{
				load.stop();
				break;
			}
		}
		
		for(int i=0; i<after_loads.Count; i++)
		{
			AssetLoad load = after_loads[i];
			if(isAllUnLoadLevel == false && load.asset.unloadLevel != Asset.UNLOAD_LEVEL.LEVEL_NORMAL)
				continue;
				
			if(load.loading == true)
			{
				load.stop();
				break;
			}
		}
		
		for(int i=0; i<loads.Count; i++)
		{
			AssetLoad load = loads[i];
			if(isAllUnLoadLevel == false && load.asset.unloadLevel != Asset.UNLOAD_LEVEL.LEVEL_NORMAL)
				continue;
				
			if(load.loading == true)
			{
				load.stop();
				break;
			}
		}
		
		isLoading = false;
		return true;
	}
	
	public bool clear(bool isAll)
	{
		stop(isAll);
		
		if(isAll == true)
		{
			before_loads.Clear();
			after_loads.Clear();
			loads.Clear();
			isLoading = false;
		}
		else
		{
			eraseByUNLoadLevel(Asset.UNLOAD_LEVEL.LEVEL_NORMAL, false);
		}
		
		return true;
	}
	    
	public void erase(Asset.TYPE type, bool removeAllOther)
	{
		while(true)
		{
			for(int i=0; i<before_loads.Count; i++)
			{
				AssetLoad load = before_loads[i];
				if((load.asset.type == type) != removeAllOther)
				{
					if(load.loading == true)
					{
						load.stop();
					}
					
					before_loads.RemoveAt(i);
					break;
				}
			}
			
			break;
		}
		
		while(true)
		{
			for(int i=0; i<after_loads.Count; i++)
			{
				AssetLoad load = after_loads[i];
				if((load.asset.type == type) != removeAllOther)
				{
					if(load.loading == true)
					{
						load.stop();
					}
					
					after_loads.RemoveAt(i);
					break;
				}
			}
			
			break;
		}
		
		while(true)
		{
			for(int i=0; i<loads.Count; i++)
			{
				AssetLoad load = loads[i];
				if((load.asset.type == type) != removeAllOther)
				{
					if(load.loading == true)
					{
						load.stop();
					}
					
					loads.RemoveAt(i);
					break;
				}
			}
			
			break;
		}
	}
	
	public void eraseByUNLoadLevel(Asset.UNLOAD_LEVEL level, bool removeAllOther)
	{
		while(true)
		{
			for(int i=0; i<before_loads.Count; i++)
			{
				AssetLoad load = before_loads[i];
				if((load.asset.unloadLevel == level) != removeAllOther)
				{
					if(load.loading == true)
					{
						load.stop();
					}
					
					before_loads.RemoveAt(i);
					break;
				}
			}
			
			break;
		}
		
		while(true)
		{
			for(int i=0; i<after_loads.Count; i++)
			{
				AssetLoad load = after_loads[i];
				if((load.asset.unloadLevel == level) != removeAllOther)
				{
					if(load.loading == true)
					{
						load.stop();
					}
					
					after_loads.RemoveAt(i);
					break;
				}
			}
			
			break;
		}
		
		while(true)
		{
			for(int i=0; i<loads.Count; i++)
			{
				AssetLoad load = loads[i];
				if((load.asset.unloadLevel == level) != removeAllOther)
				{
					if(load.loading == true)
					{
						load.stop();
					}
					
					loads.RemoveAt(i);
					break;
				}
			}
			
			break;
		}
	}
	
	public void addWatcher(SceneObject obj)
	{
		for(int i=0; i<before_loads.Count; i++)
		{
			if(before_loads[i].asset.source == obj.asset.source)
			{
				before_loads[i].addWatcher(obj);
				return;
			}
		}
		
		for(int i=0; i<after_loads.Count; i++)
		{
			if(after_loads[i].asset.source == obj.asset.source)
			{
				after_loads[i].addWatcher(obj);
				return;
			}
		}
		
		for(int i=0; i<loads.Count; i++)
		{
			if(loads[i].asset.source == obj.asset.source)
			{
				loads[i].addWatcher(obj);
				return;
			}
		}
	}
	
	public void delWatcher(SceneObject obj)
	{
		for(int i=0; i<before_loads.Count; i++)
		{
			if(before_loads[i].asset.source == obj.asset.source)
			{
				before_loads[i].delWatcher(obj);
				return;
			}
		}
		
		for(int i=0; i<after_loads.Count; i++)
		{
			if(after_loads[i].asset.source == obj.asset.source)
			{
				after_loads[i].delWatcher(obj);
				return;
			}
		}
		
		for(int i=0; i<loads.Count; i++)
		{
			if(loads[i].asset.source == obj.asset.source)
			{
				loads[i].delWatcher(obj);
				return;
			}
		}
	}
}  

