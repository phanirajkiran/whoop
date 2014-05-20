//pass
//

#include <linux/device.h>
#include <whoop.h>

struct shared {
	int resource;
	struct mutex mutex;
};

static void entrypoint(struct test_device *dev)
{
	struct shared *tp = testdev_priv(dev);
	
	for (int i = 0; i < 10; i++)
	{
		mutex_lock(&tp->mutex);
		tp->resource = 5;
		mutex_unlock(&tp->mutex);
	}
}

static int init(struct pci_dev *pdev, const struct pci_device_id *ent)
{
	struct shared *tp;
	struct test_device *dev = alloc_testdev(sizeof(*tp));
	
	tp = testdev_priv(dev);
	mutex_init(&tp->mutex);
	
	entrypoint(dev);
	
	return 0;
}

static struct test_driver test = {
	.probe = init,
	.ep1 = entrypoint
};
